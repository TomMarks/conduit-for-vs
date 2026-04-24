using System.Runtime.Serialization;
using Microsoft.VisualStudio.Extensibility.UI;

namespace Conduit.ToolWindows;

/// <summary>
/// View model for the Conduit chat tool window.
///
/// Spike-002: stub send command streams a fake response to prove the WPF Remote UI
/// update path works end-to-end.  Phase 1 replaces <see cref="ExecuteSendAsync"/>
/// with a real ICliHost integration.
/// </summary>
[DataContract]
internal sealed class ConduitToolWindowViewModel : NotifyPropertyChangedObject
{
    private string inputText = string.Empty;
    private bool canSend = true;
    private string statusText = "Ready";

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

        // Spike stub: stream a fake response word-by-word.
        // Phase 1 replaces this with real CLI stdout consumption.
        await StreamStubResponseAsync(reply, text, ct);

        reply.IsStreaming = false;
        this.CanSend = true;
        this.StatusText = "Ready";
    }

    private static async Task StreamStubResponseAsync(ChatMessage reply, string userText, CancellationToken ct)
    {
        var words = $"[stub] You said: \"{userText}\" — Phase 1 will connect this to the Claude CLI.".Split(' ');
        foreach (var word in words)
        {
            ct.ThrowIfCancellationRequested();
            reply.Text += (reply.Text.Length == 0 ? string.Empty : " ") + word;
            await Task.Delay(60, ct);
        }
    }
}
