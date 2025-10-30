using System;
using System.Collections.Generic;
using System.Globalization;

namespace BattleV2.AnimationSystem.Execution.Routers
{
    /// <summary>
    /// Lightweight parser for the timeline payload string. Supports simple identifiers
    /// and key=value pairs separated by ';' or ',' tokens.
    /// </summary>
    public readonly struct AnimationEventPayload
    {
        private static readonly char[] Separators = { ';', ',' };

        private readonly Dictionary<string, string> properties;

        private AnimationEventPayload(string raw, string identifier, Dictionary<string, string> properties)
        {
            Raw = raw;
            Identifier = identifier;
            this.properties = properties;
        }

        /// <summary>
        /// Raw payload string coming from the timeline.
        /// </summary>
        public string Raw { get; }

        /// <summary>
        /// First identifier token without '='. Fallback to 'id' property if defined.
        /// </summary>
        public string Identifier { get; }

        /// <summary>
        /// Returns true when there is no useful data.
        /// </summary>
        public bool IsEmpty => string.IsNullOrWhiteSpace(Raw) && (properties == null || properties.Count == 0);

        public IReadOnlyDictionary<string, string> Properties => properties ?? (IReadOnlyDictionary<string, string>)EmptyDictionary.Instance;

        public static AnimationEventPayload Parse(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return default;
            }

            string identifier = null;
            Dictionary<string, string> props = null;

            var segments = raw.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
            foreach (var segment in segments)
            {
                var part = segment.Trim();
                if (part.Length == 0)
                {
                    continue;
                }

                int equalsIndex = part.IndexOf('=');
                if (equalsIndex <= 0)
                {
                    if (identifier == null)
                    {
                        identifier = part;
                    }
                    else
                    {
                        props ??= NewDictionary();
                        props[$"token{props.Count}"] = part;
                    }
                    continue;
                }

                string key = part.Substring(0, equalsIndex).Trim();
                string value = part.Substring(equalsIndex + 1).Trim();
                if (key.Length == 0)
                {
                    continue;
                }

                props ??= NewDictionary();
                props[key] = value;

                if (identifier == null && string.Equals(key, "id", StringComparison.OrdinalIgnoreCase))
                {
                    identifier = value;
                }
            }

            if (identifier == null && props != null && props.TryGetValue("id", out var idFromProperty))
            {
                identifier = idFromProperty;
            }

            return new AnimationEventPayload(raw, identifier, props);
        }

        /// <summary>
        /// Resolves the most appropriate identifier for a router using the provided keys as fallbacks.
        /// </summary>
        public string ResolveId(params string[] keys)
        {
            if (!string.IsNullOrWhiteSpace(Identifier))
            {
                return Identifier;
            }

            if (keys == null || keys.Length == 0 || properties == null)
            {
                return null;
            }

            for (int i = 0; i < keys.Length; i++)
            {
                if (properties.TryGetValue(keys[i], out var value) && !string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return null;
        }

        public bool TryGetString(string key, out string value)
        {
            if (properties != null && properties.TryGetValue(key, out value))
            {
                return true;
            }

            value = null;
            return false;
        }

        public bool TryGetFloat(string key, out float value)
        {
            value = default;
            if (!TryGetString(key, out var rawValue))
            {
                return false;
            }

            return float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        public bool TryGetDouble(string key, out double value)
        {
            value = default;
            if (!TryGetString(key, out var rawValue))
            {
                return false;
            }

            return double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        public bool TryGetBool(string key, out bool value)
        {
            value = default;
            if (!TryGetString(key, out var rawValue))
            {
                return false;
            }

            return bool.TryParse(rawValue, out value);
        }

        public override string ToString() => Raw ?? string.Empty;

        private static Dictionary<string, string> NewDictionary() =>
            new Dictionary<string, string>(4, StringComparer.OrdinalIgnoreCase);

        private sealed class EmptyDictionary : IReadOnlyDictionary<string, string>
        {
            public static readonly EmptyDictionary Instance = new();

            public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
            {
                yield break;
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

            public int Count => 0;
            public bool ContainsKey(string key) => false;
            public bool TryGetValue(string key, out string value)
            {
                value = null;
                return false;
            }

            public string this[string key] => null;

            public IEnumerable<string> Keys
            {
                get { yield break; }
            }

            public IEnumerable<string> Values
            {
                get { yield break; }
            }
        }
    }
}
