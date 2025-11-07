using System;
using System.Reflection;
using UnityEngine;

namespace BattleV2.Core
{
    /// <summary>
    /// Bridge de logging para estrategias: busca BattleLogger y si no lo encuentra, cae en Debug.Log.
    /// </summary>
    internal static class StrategyLoggerBridge
    {
        private static readonly Type LoggerType = Type.GetType("BattleV2.Core.BattleLogger, Assembly-CSharp") ??
                                                  Type.GetType("BattleV2.Core.BattleLogger");

        private static readonly MethodInfo InfoM = Resolve(LoggerType, new[] { "Info", "LogInfo", "Log" });
        private static readonly MethodInfo WarnM = Resolve(LoggerType, new[] { "Warn", "Warning", "LogWarn", "LogWarning" });
        private static readonly MethodInfo ErrorM = Resolve(LoggerType, new[] { "Error", "LogError" });

        private static MethodInfo Resolve(Type type, string[] names)
        {
            if (type == null || names == null)
            {
                return null;
            }

            foreach (var name in names)
            {
                var candidate = type.GetMethod(
                    name,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                    binder: null,
                    types: new[] { typeof(string), typeof(string) },
                    modifiers: null);

                if (candidate != null)
                {
                    return candidate;
                }

                candidate = type.GetMethod(
                    name,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                    binder: null,
                    types: new[] { typeof(string) },
                    modifiers: null);

                if (candidate != null)
                {
                    return candidate;
                }
            }

            return null;
        }

        public static void Info(string scope, string message)
        {
            if (InfoM != null)
            {
                Invoke(InfoM, scope, message);
                return;
            }

            Debug.Log(Format(scope, message));
        }

        public static void Warn(string scope, string message)
        {
            if (WarnM != null)
            {
                Invoke(WarnM, scope, message);
                return;
            }

            Debug.LogWarning(Format(scope, message));
        }

        public static void Error(string scope, string message)
        {
            if (ErrorM != null)
            {
                Invoke(ErrorM, scope, message);
                return;
            }

            Debug.LogError(Format(scope, message));
        }

        private static void Invoke(MethodInfo method, string scope, string message)
        {
            if (method == null)
            {
                return;
            }

            var parameters = method.GetParameters();
            if (parameters.Length == 2)
            {
                method.Invoke(null, new object[] { scope, message });
            }
            else
            {
                method.Invoke(null, new object[] { Format(scope, message) });
            }
        }

        private static string Format(string scope, string message)
        {
            return string.IsNullOrWhiteSpace(scope) ? message : $"[{scope}] {message}";
        }
    }
}
