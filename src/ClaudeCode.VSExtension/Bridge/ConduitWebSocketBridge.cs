using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Conduit.Bridge;

/// <summary>
/// Spike-001 artifact: local HTTP + WebSocket server that bridges the WebView2 page
/// running in devenv.exe to the OOP extension host.
///
/// Remote UI has no code-behind, so we cannot handle CoreWebView2.WebMessageReceived
/// directly.  Instead, the chat page connects to this server via WebSocket; the extension
/// receives messages on <see cref="MessageReceived"/> and can push replies via
/// <see cref="BroadcastAsync"/>.
///
/// Phase 1 will promote this to a proper ICliHost + ISessionOrchestrator integration.
/// SPIKE-002 will replace the inline HTML with a virtual-host-mapped asset bundle.
/// </summary>
internal sealed class ConduitWebSocketBridge : IDisposable
{
    // Minimal chat page.  Proves two-way WebSocket communication works.
    // Replaced by a Vite bundle once SPIKE-002 closes.
    private const string ChatHtml = """
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <title>Conduit</title>
          <style>
            *    { box-sizing: border-box; margin: 0; padding: 0; }
            body { font: 13px/1.5 system-ui, sans-serif; background: #0e1116; color: #e6edf3;
                   display: flex; flex-direction: column; height: 100vh; }
            #log { flex: 1; overflow-y: auto; padding: 12px; }
            .msg { margin: 4px 0; padding: 4px 8px; border-radius: 4px; }
            .you { background: #1f2937; color: #79c0ff; }
            .ext { background: #0d1117; color: #2dd4bf; border-left: 2px solid #2dd4bf; }
            #bar { display: flex; gap: 6px; padding: 8px; border-top: 1px solid #30363d; }
            input { flex: 1; background: #161b22; color: #e6edf3; border: 1px solid #30363d;
                    padding: 6px 8px; border-radius: 4px; outline: none; }
            button{ background: #2dd4bf; color: #000; border: none; padding: 6px 12px;
                    border-radius: 4px; cursor: pointer; font-weight: 600; }
          </style>
        </head>
        <body>
          <div id="log"></div>
          <div id="bar">
            <input id="inp" placeholder="Type a message and press Enter…"
                   onkeydown="if(event.key==='Enter')send()">
            <button onclick="send()">Send</button>
          </div>
          <script>
            const ws = new WebSocket('ws://' + location.host + '/ws');
            ws.onmessage = e => append('ext', JSON.parse(e.data).text ?? '');
            ws.onerror   = () => append('ext', '\u26A0 WebSocket error');
            function append(cls, text) {
              const d = document.createElement('div');
              d.className = 'msg ' + cls;
              d.textContent = text;
              document.getElementById('log').appendChild(d);
              d.scrollIntoView();
            }
            function send() {
              const inp = document.getElementById('inp');
              const text = inp.value.trim();
              if (!text || ws.readyState !== WebSocket.OPEN) return;
              append('you', 'You: ' + text);
              ws.send(JSON.stringify({ type: 'user_message', text }));
              inp.value = '';
            }
          </script>
        </body>
        </html>
        """;

    private readonly HttpListener listener;
    private readonly CancellationTokenSource cts = new();
    private readonly List<WebSocket> connections = new();
    private readonly object connectionsLock = new();

    public int Port { get; }

    /// <summary>URL to navigate WebView2 to — includes host:port so the JS can derive the WS endpoint.</summary>
    public string SourceUrl => $"http://localhost:{this.Port}/";

    /// <summary>Fires on the thread-pool for each text frame received from any connected WebSocket client.</summary>
    public event Action<string>? MessageReceived;

    public ConduitWebSocketBridge()
    {
        this.Port = FindFreePort();
        this.listener = new HttpListener();
        this.listener.Prefixes.Add($"http://localhost:{this.Port}/");
    }

    public void Start()
    {
        this.listener.Start();
        _ = Task.Run(() => this.AcceptLoopAsync(this.cts.Token));
    }

    public async Task BroadcastAsync(string json, CancellationToken ct = default)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        var segment = new ArraySegment<byte>(bytes);

        List<WebSocket> snapshot;
        lock (this.connectionsLock)
        {
            snapshot = new List<WebSocket>(this.connections);
        }

        foreach (var ws in snapshot)
        {
            if (ws.State == WebSocketState.Open)
            {
                await ws.SendAsync(segment, WebSocketMessageType.Text, endOfMessage: true, ct);
            }
        }
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await this.listener.GetContextAsync();
            }
            catch when (ct.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                break;
            }

            _ = Task.Run(() => this.DispatchAsync(ctx, ct), ct);
        }
    }

    private async Task DispatchAsync(HttpListenerContext ctx, CancellationToken ct)
    {
        if (ctx.Request.IsWebSocketRequest)
        {
            var wsCtx = await ctx.AcceptWebSocketAsync(subProtocol: null);
            await this.HandleWebSocketAsync(wsCtx.WebSocket, ct);
            return;
        }

        await ServeHtmlAsync(ctx, ct);
    }

    private async Task HandleWebSocketAsync(WebSocket ws, CancellationToken ct)
    {
        lock (this.connectionsLock)
        {
            this.connections.Add(ws);
        }

        var buffer = new byte[8192];

        try
        {
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                if (result.MessageType != WebSocketMessageType.Text)
                {
                    continue;
                }

                var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                this.MessageReceived?.Invoke(json);

                // Spike echo: bounce the raw payload back so the exit criterion is
                // verifiable directly in the webview without a CLI process.
                var echo = JsonSerializer.Serialize(new { text = $"[echo] {json}" });
                await this.BroadcastAsync(echo, ct);
            }
        }
        finally
        {
            lock (this.connectionsLock)
            {
                this.connections.Remove(ws);
            }

            ws.Dispose();
        }
    }

    private static async Task ServeHtmlAsync(HttpListenerContext ctx, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(ChatHtml);
        ctx.Response.ContentType = "text/html; charset=utf-8";
        ctx.Response.ContentLength64 = bytes.Length;
        await ctx.Response.OutputStream.WriteAsync(bytes, ct);
        ctx.Response.OutputStream.Close();
    }

    private static int FindFreePort()
    {
        using var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    public void Dispose()
    {
        this.cts.Cancel();
        this.listener.Stop();
        this.listener.Close();
        this.cts.Dispose();
        lock (this.connectionsLock)
        {
            foreach (var ws in this.connections)
            {
                ws.Dispose();
            }

            this.connections.Clear();
        }
    }
}
