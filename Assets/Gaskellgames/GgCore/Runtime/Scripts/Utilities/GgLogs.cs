using UnityEngine;
using Object = UnityEngine.Object;

namespace Gaskellgames
{
    /// <remarks>
    /// Code created by Gaskellgames: https://gaskellgames.com
    /// </remarks>
    
    public static class GgLogs
    {
        /// <summary>
        /// Logs a message to the Unity Console.
        /// </summary>
        /// <param name="context">Object to which the message applies.</param>
        /// <param name="logType">Type of log to show in the console.</param>
        /// <param name="format">String format of the log to be shown.</param>
        /// <param name="args">Arguments to be injected to the string format.</param>
        public static void Log(Object context, GgLogType logType, string format, params object[] args)
        {
            if (!GaskellgamesSettings_SO.Instance) { return; }
            if (!GaskellgamesSettings_SO.Instance.ShowLogs) { return; }

            LogType unityLogType;
            switch (logType)
            {
                case GgLogType.Debug:
                    if (!GaskellgamesSettings_SO.Instance.ShowDebugLogs) { return; }
                    unityLogType = LogType.Log;
                    break;
                
                case GgLogType.Info:
                    if (!GaskellgamesSettings_SO.Instance.ShowInfoLogs) { return; }
                    unityLogType = LogType.Log;
                    break;
                
                case GgLogType.Warning:
                    if (!GaskellgamesSettings_SO.Instance.ShowWarningLogs) { return; }
                    unityLogType = LogType.Warning;
                    break;
                
                case GgLogType.Error:
                    if (!GaskellgamesSettings_SO.Instance.ShowErrorLogs) { return; }
                    unityLogType = LogType.Error;
                    break;
                
                case GgLogType.Assert:
                    if (!GaskellgamesSettings_SO.Instance.ShowErrorLogs) { return; }
                    unityLogType = LogType.Assert;
                    break;
                
                case GgLogType.Exception:
                    if (!GaskellgamesSettings_SO.Instance.ShowErrorLogs) { return; }
                    unityLogType = LogType.Exception;
                    break;
                
                default:
                    return;
            }
            
            string prefix = "[" + StringExtensions.AddColorRichTextTag("Gaskellgames", new Color32(000, 179, 223, 255)) + "] ";
            object message = prefix + string.Format(format, args);
            Debug.unityLogger.Log(unityLogType, message, context);
        }
        
        /// <summary>
        /// Logs a coloured message to the Unity Console.
        /// </summary>
        /// <param name="messageColor">Color of the message to display in the console.</param>
        /// <param name="context">Object to which the message applies.</param>
        /// <param name="logType">Type of log to show in the console.</param>
        /// <param name="format">String format of the log to be shown.</param>
        /// <param name="args">Arguments to be injected to the string format.</param>
        public static void Log(Color32 messageColor, Object context, GgLogType logType, string format, params object[] args)
        {
            if (!GaskellgamesSettings_SO.Instance) { return; }
            if (!GaskellgamesSettings_SO.Instance.ShowLogs) { return; }
            
            LogType unityLogType;
            switch (logType)
            {
                case GgLogType.Debug:
                    if (!GaskellgamesSettings_SO.Instance.ShowDebugLogs) { return; }
                    unityLogType = LogType.Log;
                    break;
                
                case GgLogType.Info:
                    if (!GaskellgamesSettings_SO.Instance.ShowInfoLogs) { return; }
                    unityLogType = LogType.Log;
                    break;
                
                case GgLogType.Warning:
                    if (!GaskellgamesSettings_SO.Instance.ShowWarningLogs) { return; }
                    unityLogType = LogType.Warning;
                    break;
                
                case GgLogType.Error:
                    if (!GaskellgamesSettings_SO.Instance.ShowErrorLogs) { return; }
                    unityLogType = LogType.Error;
                    break;
                
                case GgLogType.Assert:
                    if (!GaskellgamesSettings_SO.Instance.ShowErrorLogs) { return; }
                    unityLogType = LogType.Assert;
                    break;
                
                case GgLogType.Exception:
                    if (!GaskellgamesSettings_SO.Instance.ShowErrorLogs) { return; }
                    unityLogType = LogType.Exception;
                    break;
                
                default:
                    return;
            }

            string prefix = "[" + StringExtensions.AddColorRichTextTag("Gaskellgames", new Color32(000, 179, 223, 255)) + "] ";
            object message = prefix + StringExtensions.AddColorRichTextTag(string.Format(format, args), messageColor);
            Debug.unityLogger.Log(unityLogType, message, context);
        }

    } // class end
}
