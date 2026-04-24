using System.Runtime.Serialization;
using Conduit.Cli;
using Conduit.Cli.Events;
using Microsoft.VisualStudio.Extensibility.UI;

namespace Conduit.ToolWindows;

/// <summary>
/// View model for the Conduit chat tool window.
///
/// Manages the chat message list and wires user input to the Claude CLI subprocess
/// via <see cref="CliProcessHost"/>.  Session context is preserved across turns
/// using the session ID emitted in the first <see cref="SystemInitEvent"/>.
/// </summary>
[DataContract]
internal sealed class ConduitToolWindowViewModel : NotifyPropertyChangedObject
{
    private string inputText = string.Empty;
    private bool canSend = true;
    private string statusText = "Ready";
    private string? sessionId;

    public ConduitToolWindowViewModel()
    {
        this.Messages = [];
        this.SendCommand = new AsyncCommand(this.ExecuteSendAsync);
    }

    [DataMember]
    public ObservableList<ChatMessage> Messages { get; }

    [DataMember]
    public string InputText
    {
        get => this.inputText;
        set => this.SetProperty(ref this.inputText, value);
    }

    [DataMember]
    public bool CanSend
    {
        get => this.canSend;
        private set => this.SetProperty(ref this.canSend, value);
    }

    [DataMember]
    public string StatusText
    {
        get => this.statusText;
        private set => this.SetProperty(ref this.statusText, value);
    }

    [DataMember]
    public IAsyncCommand SendCommand { get; }

    private async Task ExecuteSendAsync(object? parameter, CancellationToken ct)
    {
        var text = this.InputText.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        this.InputText = string.Empty;
        this.CanSend = false;
        this.StatusText = "Claude is thinking\u2026";

        this.Messages.Add(new ChatMessage { IsUser = true, Text = text });

        var reply = new ChatMessage { IsUser = false, IsStreaming = true };
        this.Messages.Add(reply);

        try
        {
            await foreach (var evt in CliProcessHost.RunAsync(text, this.sessionId, ct))
            {
                switch (evt)
                {
                    case SystemInitEvent init:
                        this.sessionId = init.SessionId;
                        this.StatusText = $"Connected \u00b7 {init.Model}";
                        break;

                    case TextTokenEvent token:
                        reply.Text += token.Text;
                        break;

                    case SessionCompleteEvent { IsError: true } error:
                        reply.Text = string.IsNullOrWhiteSpace(reply.Text)
                            ? error.ResultText
                            : reply.Text;
                        this.StatusText = "Error \u2014 see message above";
                        break;

                    case SessionCompleteEvent complete:
                        this.StatusText = $"Ready \u00b7 {complete.NumTurns} turn(s) \u00b7 ${complete.CostUsd:F4}";
                        break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            reply.Text = string.IsNullOrWhiteSpace(reply.Text)
                ? "[cancelled]"
                : reply.Text + " [cancelled]";
            this.StatusText = "Cancelled";
        }
        catch (Exception ex)
        {
            reply.Text = $"Failed to start Claude CLI: {ex.Message}\n\nEnsure 'claude' is installed and on the PATH.";
            this.StatusText = "Error \u2014 CLI not found";
        }
        finally
        {
            reply.IsStreaming = false;
            this.CanSend = true;
        }
    }
}
