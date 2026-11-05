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

namespace FlixelDebugger.Protocol;

/// <summary>
/// Numeric constants of the remote debug wire protocol.
/// </summary>
/// <remarks>
/// <para>
/// Think of this class as the phrasebook two travelers share: the game and the app never share code,
/// but both agree that 3 means "panel list" and 6 means "ping". Every value here matches a row in
/// <c>DEBUG_PROTOCOL.md</c> of the FlixelGDX framework repository, which is the single source of
/// truth, and the same value in the Java <c>FlixelDebugProtocol</c> class.
/// </para>
/// <para>
/// Codes that name a whole category are enums in this namespace: <see cref="MessageType"/>,
/// <see cref="PanelKind"/>, <see cref="ValueKind"/>, <see cref="LogLevel"/> and
/// <see cref="ConsoleStatus"/>. This class holds the version, the limits, the payload subtypes
/// (<see cref="Subtypes"/>) and the action codes (<see cref="Actions"/>). Codes are never reused; a
/// retired code stays reserved forever.
/// </para>
/// </remarks>
public static class ProtocolConstants
{
    /// <summary>The protocol major version. The app closes the connection when the majors differ.</summary>
    public const ushort VersionMajor = 1;

    /// <summary>The protocol minor version. Minors may differ freely.</summary>
    public const ushort VersionMinor = 0;

    /// <summary>The default port the app listens on.</summary>
    public const int DefaultPort = 6510;

    /// <summary>How long the game waits between reconnect attempts, in milliseconds.</summary>
    public const int ReconnectMillis = 2000;

    /// <summary>The most bytes a string can hold on the wire.</summary>
    public const int MaxStringBytes = 65535;

    /// <summary>The most elements an array can hold on the wire.</summary>
    public const int MaxArrayCount = 65535;

    /// <summary>The most characters the game keeps of one log message before cutting it off.</summary>
    public const int MaxLogMessageChars = 2000;

    /// <summary>The most characters a console line may have; the game rejects longer lines.</summary>
    public const int MaxCommandChars = 500;

    /// <summary>The default WebSocket URL a game connects to.</summary>
    public const string DefaultUrl = "ws://127.0.0.1:6510/";

    /// <summary>
    /// Payload subtype codes, for the panels that have more than one payload kind.
    /// </summary>
    public static class Subtypes
    {
        /// <summary>Subtypes of the textures panel.</summary>
        public static class Textures
        {
            /// <summary>The list of textures, game to app.</summary>
            public const byte List = 1;

            /// <summary>One texture thumbnail, game to app.</summary>
            public const byte Thumbnail = 2;
        }

        /// <summary>Subtypes of the console panel.</summary>
        public static class Console
        {
            /// <summary>The list of commands, game to app.</summary>
            public const byte Commands = 1;

            /// <summary>The result of a command, game to app.</summary>
            public const byte Result = 2;
        }

        /// <summary>Subtypes of the inspector panel.</summary>
        public static class Inspector
        {
            /// <summary>The state of the selection, game to app.</summary>
            public const byte State = 1;

            /// <summary>One object thumbnail, game to app.</summary>
            public const byte Thumbnail = 2;
        }
    }

    /// <summary>
    /// Action codes. They are scoped to a panel kind, so 1 means different things on different panels.
    /// </summary>
    public static class Actions
    {
        /// <summary>Actions of the logs panel.</summary>
        public static class Logs
        {
            /// <summary>Clears the log.</summary>
            public const byte Clear = 1;
        }

        /// <summary>Actions of the textures panel.</summary>
        public static class Textures
        {
            /// <summary>Asks for a thumbnail of one texture.</summary>
            public const byte RequestThumbnail = 1;
        }

        /// <summary>Actions of the controls panel.</summary>
        public static class Controls
        {
            /// <summary>Pauses or resumes the game.</summary>
            public const byte SetPaused = 1;

            /// <summary>Advances one frame while paused.</summary>
            public const byte Step = 2;

            /// <summary>Shows or hides hitboxes.</summary>
            public const byte SetHitboxes = 3;

            /// <summary>Turns the free camera on or off.</summary>
            public const byte SetFreeCam = 4;

            /// <summary>Sets the sample rate in Hz.</summary>
            public const byte SetRate = 5;

            /// <summary>Sets the log level override.</summary>
            public const byte SetLogLevel = 6;
        }

        /// <summary>Actions of the console panel.</summary>
        public static class Console
        {
            /// <summary>Runs a console line.</summary>
            public const byte Run = 1;
        }

        /// <summary>Actions of the scene panel.</summary>
        public static class Scene
        {
            /// <summary>Asks for a fresh tree snapshot.</summary>
            public const byte RequestTree = 1;
        }

        /// <summary>Actions of the inspector panel.</summary>
        public static class Inspector
        {
            /// <summary>Selects an object by id.</summary>
            public const byte Select = 1;

            /// <summary>Selects an object by path.</summary>
            public const byte SelectPath = 2;

            /// <summary>Selects the parent of the selection.</summary>
            public const byte SelectParent = 3;

            /// <summary>Clears the selection.</summary>
            public const byte Clear = 4;

            /// <summary>Moves the game camera to the selection.</summary>
            public const byte GoTo = 5;

            /// <summary>Edits one property of the selection.</summary>
            public const byte SetProperty = 6;

            /// <summary>Asks for a thumbnail of one object.</summary>
            public const byte RequestThumbnail = 7;
        }

        /// <summary>Actions of the save panel.</summary>
        public static class Save
        {
            /// <summary>Sets one save value from text.</summary>
            public const byte Set = 1;

            /// <summary>Resets one save key.</summary>
            public const byte ResetKey = 2;

            /// <summary>Writes unsaved changes now.</summary>
            public const byte SaveNow = 3;

            /// <summary>Resets every save key.</summary>
            public const byte ResetAll = 4;
        }

        /// <summary>Actions of custom panels.</summary>
        public static class Custom
        {
            /// <summary>Presses a button.</summary>
            public const byte Press = 1;

            /// <summary>Edits one property row.</summary>
            public const byte SetProperty = 2;
        }
    }
}
