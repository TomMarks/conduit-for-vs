#pragma warning disable VSEXTPREVIEW_SETTINGS  // Settings API is preview in SDK 17.14

using System.Runtime.Serialization;
using Conduit.Cli;
using Conduit.Cli.Events;
using Conduit.Settings;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.UI;

namespace Conduit.ToolWindows;

/// <summary>
/// View model for the Conduit chat tool window.
///
/// Manages the chat message list and wires user input to the Claude CLI subprocess
/// via <see cref="CliProcessHost"/>.  Session context is preserved across turns
/// using the session ID emitted in the first <see cref="SystemInitEvent"/>.
///
/// Auth errors (<see cref="SessionCompleteEvent.IsError"/> with an auth-related
/// message) are surfaced as an inline sign-in banner rather than a raw error string.
/// <see cref="SignInCommand"/> opens a console window running <c>claude /login</c>
/// via <see cref="ClaudeAuthLauncher"/>.
///
/// The active provider is read from <see cref="ConduitSettings.Provider"/> on every
/// send so that changes made in Tools &gt; Options take effect without restarting VS.
/// </summary>
[DataContract]
internal sealed class ConduitToolWindowViewModel : NotifyPropertyChangedObject
{
    private readonly VisualStudioExtensibility extensibility;

    private string inputText = string.Empty;
    private bool canSend = true;
    private string statusText = "Ready";
    private bool showSignIn;
    private string? sessionId;

    public ConduitToolWindowViewModel(VisualStudioExtensibility extensibility)
    {
        this.extensibility = extensibility;
        this.Messages = [];
        this.SendCommand = new AsyncCommand(this.ExecuteSendAsync);
        this.SignInCommand = new AsyncCommand(this.ExecuteSignInAsync);
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

    /// <summary>
    /// When <see langword="true"/>, the sign-in banner is shown in the UI.
    /// Set after an auth error; cleared when the user initiates sign-in or sends again.
    /// </summary>
    [DataMember]
    public bool ShowSignIn
    {
        get => this.showSignIn;
        private set => this.SetProperty(ref this.showSignIn, value);
    }

    [DataMember]
    public IAsyncCommand SendCommand { get; }

    /// <summary>
    /// Opens a console window running <c>claude /login</c> via
    /// <see cref="ClaudeAuthLauncher"/>.
    /// </summary>
    [DataMember]
    public IAsyncCommand SignInCommand { get; }

    private Task ExecuteSignInAsync(object? parameter, CancellationToken ct)
    {
        this.ShowSignIn = false;

        if (!ClaudeAuthLauncher.LaunchLogin())
        {
            this.StatusText = "Could not open a terminal \u2014 run 'claude /login' manually.";
        }
        else
        {
            this.StatusText = "Sign-in window opened \u2014 return here after authenticating.";
        }

        return Task.CompletedTask;
    }

    private async Task ExecuteSendAsync(object? parameter, CancellationToken ct)
    {
        var text = this.InputText.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        this.InputText = string.Empty;
        this.CanSend = false;
        this.ShowSignIn = false;
        this.StatusText = "Claude is thinking\u2026";

        this.Messages.Add(new ChatMessage { IsUser = true, Text = text });

        var reply = new ChatMessage { IsUser = false, IsStreaming = true };
        this.Messages.Add(reply);

        try
        {
            // Read the provider setting on every send so Tools > Options changes
            // take effect without restarting Visual Studio.
            var providerResult = await this.extensibility.Settings()
                .ReadEffectiveValueAsync(ConduitSettings.Provider, ct);
            var providerConfig = ProviderConfig.FromSettingValue(providerResult.ValueOrDefault("anthropic"));

            await foreach (var evt in CliProcessHost.RunAsync(text, this.sessionId, providerConfig, ct))
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
                        if (IsAuthError(error.ResultText))
                        {
                            reply.Text = "Claude is not authenticated. Use the Sign In button below to open a login window, then send your message again.";
                            this.StatusText = "Not signed in";
                            this.ShowSignIn = true;
                        }
                        else
                        {
                            reply.Text = string.IsNullOrWhiteSpace(reply.Text)
                                ? error.ResultText
                                : reply.Text;
                            this.StatusText = "Error \u2014 see message above";
                        }

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

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="resultText"/> matches
    /// known authentication-failure patterns emitted by the Claude CLI.
    /// </summary>
    private static bool IsAuthError(string resultText)
    {
        return resultText.Contains("login", StringComparison.OrdinalIgnoreCase)
            || resultText.Contains("log in", StringComparison.OrdinalIgnoreCase)
            || resultText.Contains("not logged in", StringComparison.OrdinalIgnoreCase)
            || resultText.Contains("api key", StringComparison.OrdinalIgnoreCase)
            || resultText.Contains("authentication", StringComparison.OrdinalIgnoreCase)
            || resultText.Contains("unauthorized", StringComparison.OrdinalIgnoreCase)
            || resultText.Contains("unauthenticated", StringComparison.OrdinalIgnoreCase);
    }
}
