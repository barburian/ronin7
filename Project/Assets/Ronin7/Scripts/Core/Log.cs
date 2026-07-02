using System.Diagnostics;
using UnityEngine;

namespace Ronin7.Core
{
    /// <summary>
    /// Compile-time-stripped logger for gameplay code. Calls are removed by the C# compiler
    /// in builds that do not define <c>DEBUG_SS</c> (Project Settings → Player → Scripting
    /// Define Symbols), so prod builds pay zero cost — no string formatting, no method call.
    ///
    /// Use <see cref="Info"/> for one-shot lifecycle/state-change logs (wave start, objective
    /// complete) and <see cref="Verbose"/> for high-frequency / hot-path logs (per-impact,
    /// per-frame). LogError / LogWarning calls should keep using <see cref="UnityEngine.Debug"/>
    /// directly — those need to fire in prod.
    /// </summary>
    public static class Log
    {
        [Conditional("DEBUG_SS")]
        public static void Info(string msg) => UnityEngine.Debug.Log(msg);

        [Conditional("DEBUG_SS")]
        public static void Info(string msg, Object context) => UnityEngine.Debug.Log(msg, context);

        [Conditional("DEBUG_SS")]
        public static void Verbose(string msg) => UnityEngine.Debug.Log(msg);

        [Conditional("DEBUG_SS")]
        public static void Verbose(string msg, Object context) => UnityEngine.Debug.Log(msg, context);
    }
}
