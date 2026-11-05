// MIT License
//
// Copyright (c) 2026 stringdotjar
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using A = FlixelDebugger.Protocol.ProtocolConstants.Actions;

namespace FlixelDebugger.Protocol.Actions;

/// <summary>
/// The decoded body of a PANEL_ACTION message. Its concrete type depends on the panel kind and the action code.
/// </summary>
/// <remarks>
/// Action codes are scoped to the panel kind, so code 1 means different things on different panels.
/// Codes this library does not know decode to <see cref="UnknownAction"/>, as the protocol requires.
/// </remarks>
public abstract record ActionPayload
{
  /// <summary>
  /// Gets the action code byte that identifies this action within its panel kind.
  /// </summary>
  public abstract byte Code { get; }

  /// <summary>
  /// Writes the fields of this action. The code byte itself is written by the message.
  /// </summary>
  /// <param name="writer">The writer to append to.</param>
  public abstract void Write(MessageWriter writer);

  /// <summary>
  /// Reads an action: its code byte followed by its fields.
  /// </summary>
  /// <param name="kind">The kind of the panel, or <see langword="null"/> if the panel index is not known.</param>
  /// <param name="reader">The reader, positioned just after the panel index.</param>
  /// <returns>The action, or an <see cref="UnknownAction"/> if the kind or code is not understood.</returns>
  /// <exception cref="ProtocolException">The message is too short.</exception>
  public static ActionPayload Read(PanelKind? kind, ref MessageReader reader)
  {
    var code = reader.ReadU8();
    var action = kind switch
    {
      PanelKind.Custom => ReadCustom(code, ref reader),
      PanelKind.Logs => code == A.Logs.Clear ? new LogsClearAction() : null,
      PanelKind.Textures => code == A.Textures.RequestThumbnail ? TexturesRequestThumbnailAction.Read(ref reader) : null,
      PanelKind.Controls => ReadControls(code, ref reader),
      PanelKind.Console => code == A.Console.Run ? ConsoleRunAction.Read(ref reader) : null,
      PanelKind.Scene => code == A.Scene.RequestTree ? new SceneRequestTreeAction() : null,
      PanelKind.Inspector => ReadInspector(code, ref reader),
      PanelKind.Save => ReadSave(code, ref reader),
      _ => null,
    };

    return action ?? new UnknownAction(code, reader.ReadRemaining().ToArray());
  }

  private static ActionPayload? ReadCustom(byte code, ref MessageReader reader) => code switch
  {
    A.Custom.Press => CustomPressAction.Read(ref reader),
    A.Custom.SetProperty => CustomSetPropertyAction.Read(ref reader),
    _ => null,
  };

  private static ActionPayload? ReadControls(byte code, ref MessageReader reader) => code switch
  {
    A.Controls.SetPaused => ControlsSetPausedAction.Read(ref reader),
    A.Controls.Step => new ControlsStepAction(),
    A.Controls.SetHitboxes => ControlsSetHitboxesAction.Read(ref reader),
    A.Controls.SetFreeCam => ControlsSetFreeCamAction.Read(ref reader),
    A.Controls.SetRate => ControlsSetRateAction.Read(ref reader),
    A.Controls.SetLogLevel => ControlsSetLogLevelAction.Read(ref reader),
    _ => null,
  };

  private static ActionPayload? ReadInspector(byte code, ref MessageReader reader) => code switch
  {
    A.Inspector.Select => InspectorSelectAction.Read(ref reader),
    A.Inspector.SelectPath => InspectorSelectPathAction.Read(ref reader),
    A.Inspector.SelectParent => new InspectorSelectParentAction(),
    A.Inspector.Clear => new InspectorClearAction(),
    A.Inspector.GoTo => new InspectorGoToAction(),
    A.Inspector.SetProperty => InspectorSetPropertyAction.Read(ref reader),
    A.Inspector.RequestThumbnail => InspectorRequestThumbnailAction.Read(ref reader),
    _ => null,
  };

  private static ActionPayload? ReadSave(byte code, ref MessageReader reader) => code switch
  {
    A.Save.Set => SaveSetAction.Read(ref reader),
    A.Save.ResetKey => SaveResetKeyAction.Read(ref reader),
    A.Save.SaveNow => new SaveSaveNowAction(),
    A.Save.ResetAll => new SaveResetAllAction(),
    _ => null,
  };
}
