using System.Runtime.Serialization;
using Microsoft.VisualStudio.Extensibility.UI;

namespace Conduit.ToolWindows;

/// <summary>
/// A single turn in the chat.  <see cref="Text"/> is mutated in-place during streaming
/// so each token update propagates to the Remote UI without re-creating the item.
/// </summary>
[DataContract]
internal sealed class ChatMessage : NotifyPropertyChangedObject
{
    private string text = string.Empty;
    private bool isStreaming;

    /// <summary>True for a user-authored message; false for an assistant response.</summary>
    [DataMember]
    public bool IsUser { get; init; }

    /// <summary>Message body.  Grows token-by-token while <see cref="IsStreaming"/> is true.</summary>
    [DataMember]
    public string Text
    {
        get => this.text;
        set => this.SetProperty(ref this.text, value);
    }

    /// <summary>True while the assistant is still generating this message.</summary>
    [DataMember]
    public bool IsStreaming
    {
        get => this.isStreaming;
        set => this.SetProperty(ref this.isStreaming, value);
    }
}
