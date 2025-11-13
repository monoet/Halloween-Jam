using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleV2.Debugging
{
    /// <summary>
    /// Lightweight developer console with registerable text commands. Toggled with the backquote key.
    /// </summary>
    public sealed class DevConsole : MonoBehaviour
    {
        private const int MaxLogEntries = 128;

        private static DevConsole instance;

        [SerializeField] private KeyCode toggleKey = KeyCode.BackQuote;
        [SerializeField] private Vector2 windowSize = new Vector2(520f, 260f);

        private readonly Dictionary<string, CommandInfo> commands = new Dictionary<string, CommandInfo>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> outputLog = new List<string>(MaxLogEntries);
        private readonly List<string> history = new List<string>(32);
        private Vector2 scroll;
        private bool isVisible;
        private string inputBuffer = string.Empty;
        private int historyIndex = -1;
        private Rect windowRect = new Rect(16f, 16f, 520f, 260f);

        private DevConsole()
        {
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                isVisible = !isVisible;
                if (isVisible)
                {
                    historyIndex = history.Count;
                }
            }

            if (!isVisible)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                SubmitInput();
            }
            else if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                NavigateHistory(-1);
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                NavigateHistory(1);
            }
        }

        private void OnGUI()
        {
            if (!isVisible)
            {
                return;
            }

            windowRect = GUILayout.Window(
                GetInstanceID(),
                windowRect,
                DrawWindowContents,
                "Dev Console");
        }

        private void DrawWindowContents(int windowId)
        {
            GUILayout.BeginVertical();

            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(windowSize.y - 70f));
            for (int i = 0; i < outputLog.Count; i++)
            {
                GUILayout.Label(outputLog[i]);
            }
            GUILayout.EndScrollView();

            GUI.SetNextControlName("DevConsoleInput");
            inputBuffer = GUILayout.TextField(inputBuffer);
            GUI.FocusControl("DevConsoleInput");

            if (Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
            {
                SubmitInput();
                Event.current.Use();
            }

            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private void SubmitInput()
        {
            var raw = inputBuffer?.Trim();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return;
            }

            AddToHistory(raw);
            Execute(raw);
            inputBuffer = string.Empty;
            scroll.y = float.MaxValue;
        }

        private void Execute(string raw)
        {
            var tokens = raw.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
            {
                return;
            }

            var command = tokens[0];
            var args = new string[tokens.Length - 1];
            Array.Copy(tokens, 1, args, 0, args.Length);

            if (commands.TryGetValue(command, out var info))
            {
                try
                {
                    info.Handler?.Invoke(args);
                }
                catch (Exception ex)
                {
                    Log($"Command '{command}' threw: {ex.Message}");
                    Debug.LogException(ex);
                }
            }
            else
            {
                Log($"Unknown command '{command}'.");
            }
        }

        private void AddToHistory(string entry)
        {
            history.Add(entry);
            if (history.Count > 100)
            {
                history.RemoveAt(0);
            }

            historyIndex = history.Count;
        }

        private void NavigateHistory(int delta)
        {
            if (history.Count == 0)
            {
                return;
            }

            historyIndex = Mathf.Clamp(historyIndex + delta, 0, history.Count - 1);
            inputBuffer = history[historyIndex];
            GUI.FocusControl("DevConsoleInput");
        }

        private void AppendLog(string message)
        {
            if (outputLog.Count >= MaxLogEntries)
            {
                outputLog.RemoveAt(0);
            }

            outputLog.Add(message);
            scroll.y = float.MaxValue;
        }

        private static DevConsole EnsureInstance()
        {
            if (instance != null)
            {
                return instance;
            }

            var go = new GameObject("[DevConsole]");
            instance = go.AddComponent<DevConsole>();
            DontDestroyOnLoad(go);
            return instance;
        }

        private static void EnsureBootstrap()
        {
            EnsureInstance();
        }

        public static void RegisterCommand(string id, string description, Action<string[]> handler)
        {
            if (string.IsNullOrWhiteSpace(id) || handler == null)
            {
                return;
            }

            var console = EnsureInstance();
            console.commands[id] = new CommandInfo(id, description, handler);
        }

        public static void Log(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            EnsureInstance().AppendLog(message);
            Debug.Log($"[DevConsole] {message}");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RuntimeInit()
        {
            EnsureBootstrap();
        }

        private sealed class CommandInfo
        {
            public string Id { get; }
            public string Description { get; }
            public Action<string[]> Handler { get; }

            public CommandInfo(string id, string description, Action<string[]> handler)
            {
                Id = id;
                Description = description;
                Handler = handler;
            }
        }
    }
}
