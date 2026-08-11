using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace Gaskellgames
{
    #region Base GgEvent class

    [Serializable]
    public abstract class GgEventBase 
    {
        [SerializeField]
        [Tooltip("If debug is true: info message logs will be displayed in the console. [GgLogs.Info must also be enabled!]")]
        internal bool verboseLogs = false;
        
        [SerializeField]
        [Tooltip("The colour of the verbose logs.")]
        public Color32 logColor = InspectorExtensions.textNormalColor;
        
        [SerializeField]
        [Tooltip("InstanceName used to set the label in the inspector.")]
        internal string instanceName = "";
        
        [SerializeField, Min(0)]
        [Tooltip("Delay in seconds from this event being called, to this event being invoked.")]
        internal float delay;
        
        /// <summary>
        /// Get/Set the delay in seconds from this event being called, to this event being invoked
        /// </summary>
        public float Delay
        {
            get => delay;
            set => delay = value < 0 ? 0 : value;
        }
        
        protected CancellationTokenSource TokenSource = new CancellationTokenSource();
        
        /// <summary>
        /// Returns the type information for all args.
        /// </summary>
        /// <returns></returns>
        public abstract Type[] GetArgTypes();
        
        /// <summary>
        /// Invoke all registered callbacks (runtime and persistent) using default args.
        /// </summary>
        internal abstract void InvokeEventWithDefaultArgs();
        
        /// <summary>
        /// Cancel any outstanding delayed invoked callbacks (runtime and persistent).
        /// </summary>
        public void CancelInvoke()
        {
            TokenSource?.Cancel();
            TokenSource = new CancellationTokenSource();
        }
        
        /// <summary>
        /// Remove all non-persistent (ie created from script) listeners from the event.
        /// </summary>
        public abstract void RemoveAllListeners();
        
        /// <summary>
        /// Get the number of registered persistent listeners.
        /// </summary>
        /// <returns></returns>
        public abstract int GetPersistentEventCount();
        
        /// <summary>
        /// Get the target component of the listener at index index.
        /// </summary>
        /// <param name="index">Index of the listener to query.</param>
        /// <returns></returns>
        public abstract Object GetPersistentTarget(int index);
        
        /// <summary>
        /// Get the target method name of the listener at index index.
        /// </summary>
        /// <param name="index">Index of the listener to query.</param>
        /// <returns></returns>
        public abstract string GetPersistentMethodName(int index);
        
        /// <summary>
        /// Returns the execution state of a persistent listener
        /// </summary>
        /// <param name="index">Index of the listener to query.</param>
        /// <returns>Execution state of the persistent listener.</returns>
        public abstract UnityEventCallState GetPersistentListenerState(int index);
        
        /// <summary>
        /// Draw all persistent events as gizmo connections from the specified transform.
        /// </summary>
        /// <param name="thisTransform"></param>
        /// <param name="arrows"></param>
        public void DrawConnectionGizmos(Transform thisTransform, bool arrows = false)
        {
            // for each persistent listener, draw link line
            for (int i = 0; i < GetPersistentEventCount(); i++)
            {
                Object target = GetPersistentTarget(i);
                Component component = target as Component;
                if (component)
                {
                    if (arrows) { GizmosExtensions.DrawArrowLine(thisTransform.position, component.transform.position); }
                    else { Gizmos.DrawLine(thisTransform.position, component.transform.position); }
                }
                else
                {
                    GameObject go = target as GameObject;
                    if (go)
                    {
                        if (arrows) { GizmosExtensions.DrawArrowLine(thisTransform.position, go.transform.position); }
                        else { Gizmos.DrawLine(thisTransform.position, go.transform.position); }
                    }
                }
            }
        }
        
    }

    #endregion
    
    //----------------------------------------------------------------------------------------------------
    
    #region zero argument GgEvent class

    /// <summary>
    /// A zero argument persistent callback that can be saved with the Scene.
    /// </summary>
    [Serializable]
    public class GgEvent : GgEventBase
    {
        [SerializeField]
        [Tooltip("UnityEvent used as a base to allow subscribing to and invoking GGEvents")]
        private UnityEvent unityEvent;
        
        /// <summary>
        /// Invoke all registered callbacks (runtime and persistent).
        /// </summary>
        public async void Invoke()
        {
            UnityEvent instanceEvent = unityEvent;
            if (verboseLogs && 0 < delay) { GgLogs.Log(logColor, null, GgLogType.Info, "{0} waiting for {1} seconds.", instanceName, delay); }
            
            TaskResultType waitResult = 0 < delay ? await GgTask.WaitForSeconds(delay, TokenSource) : TaskResultType.Complete;
            if (instanceEvent == null) { return; }
            switch (waitResult)
            {
                default:
                case TaskResultType.Failed:
                    if (verboseLogs) { GgLogs.Log(logColor, null, GgLogType.Info, "Unity Event '{0}' Failed.", instanceName); }
                    return;
                
                case TaskResultType.Timeout:
                    if (verboseLogs) { GgLogs.Log(logColor, null, GgLogType.Info, "Unity Event '{0}' Timeout.", instanceName); }
                    return;
                    
                case TaskResultType.Cancelled:
                    if (verboseLogs) { GgLogs.Log(logColor, null, GgLogType.Info, "Unity Event '{0}' Cancelled.", instanceName); }
                    return;
                    
                case TaskResultType.Complete:
                    if (verboseLogs) { GgLogs.Log(logColor, null, GgLogType.Info, "Unity Event '{0}' Invoked.", instanceName); }
                    instanceEvent?.Invoke();
                    break;
            }
        }
        
        /// <summary>
        /// Returns the type information for all args.
        /// </summary>
        /// <returns></returns>
        public override Type[] GetArgTypes() => null;
        
        /// <summary>
        /// Invoke all registered callbacks (runtime and persistent) using default args.
        /// </summary>
        internal override void InvokeEventWithDefaultArgs() => Invoke();
        
        /// <summary>
        /// Add a non-persistent listener to the UnityEvent.
        /// </summary>
        /// <param name="call"></param>
        public void AddListener(UnityAction call) => unityEvent.AddListener(call);
        
        /// <summary>
        /// Remove a non-persistent listener from the UnityEvent. If you have added the same listener multiple times, this method will remove all occurrences of it.
        /// </summary>
        /// <param name="call"></param>
        public void RemoveListener(UnityAction call) => unityEvent.RemoveListener(call);
        
        /// <summary>
        /// Remove all non-persistent (i.e. created from script) listeners from the event.
        /// </summary>
        public override void RemoveAllListeners() => unityEvent.RemoveAllListeners();
        
        /// <summary>
        /// Get the number of registered persistent listeners.
        /// </summary>
        /// <returns></returns>
        public override int GetPersistentEventCount() => unityEvent.GetPersistentEventCount();
        
        /// <summary>
        /// Get the target component of the listener at index index.
        /// </summary>
        /// <param name="index">Index of the listener to query.</param>
        /// <returns></returns>
        public override Object GetPersistentTarget(int index) => unityEvent.GetPersistentTarget(index);
        
        /// <summary>
        /// Get the target method name of the listener at index index.
        /// </summary>
        /// <param name="index">Index of the listener to query.</param>
        /// <returns></returns>
        public override string GetPersistentMethodName(int index) => unityEvent.GetPersistentMethodName(index);
        
        /// <summary>
        /// Returns the execution state of a persistent listener
        /// </summary>
        /// <param name="index">Index of the listener to query.</param>
        /// <returns>Execution state of the persistent listener.</returns>
        public override UnityEventCallState GetPersistentListenerState(int index) => unityEvent.GetPersistentListenerState(index);
        
    }

    #endregion
    
    //----------------------------------------------------------------------------------------------------
    
    #region single argument GgEvent class

    /// <summary>
    /// A single argument version of GGEvent.
    /// </summary>
    [Serializable]
    public class GgEvent<T0> : GgEventBase
    {
        [SerializeField]
        [Tooltip("UnityEvent used as a base to allow subscribing to and invoking GGEvents")]
        private UnityEvent<T0> unityEvent;
        
        /// <summary>
        /// Invoke all registered callbacks (runtime and persistent).
        /// </summary>
        public async void Invoke(T0 arg0)
        {
            UnityEvent<T0> instanceEvent = unityEvent;
            if (verboseLogs && 0 < delay) { GgLogs.Log(logColor, null, GgLogType.Info, "{0} waiting for {1} seconds.", instanceName, delay); }
            
            TaskResultType waitResult = 0 < delay ? await GgTask.WaitForSeconds(delay, TokenSource) : TaskResultType.Complete;
            if (instanceEvent == null) { return; }
            switch (waitResult)
            {
                default:
                case TaskResultType.Failed:
                    if (verboseLogs) { GgLogs.Log(logColor, null, GgLogType.Info, "Unity Event '{0}' Failed.", instanceName); }
                    return;
                
                case TaskResultType.Timeout:
                    if (verboseLogs) { GgLogs.Log(logColor, null, GgLogType.Info, "Unity Event '{0}' Timeout.", instanceName); }
                    return;
                    
                case TaskResultType.Cancelled:
                    if (verboseLogs) { GgLogs.Log(logColor, null, GgLogType.Info, "Unity Event '{0}' Cancelled.", instanceName); }
                    return;
                    
                case TaskResultType.Complete:
                    if (verboseLogs) { GgLogs.Log(logColor, null, GgLogType.Info, "Unity Event '{0}' Invoked.", instanceName); }
                    instanceEvent?.Invoke(arg0);
                    break;
            }
        }
        
        /// <summary>
        /// Returns the type information for all args.
        /// </summary>
        /// <returns></returns>
        public override Type[] GetArgTypes() => typeof(UnityEvent<T0>).GetGenericArguments();
        
        /// <summary>
        /// Invoke all registered callbacks (runtime and persistent) using default args.
        /// </summary>
        internal override void InvokeEventWithDefaultArgs() => Invoke(default);
        
        /// <summary>
        /// Add a non-persistent listener to the UnityEvent.
        /// </summary>
        /// <param name="call"></param>
        public void AddListener(UnityAction<T0> call) => unityEvent.AddListener(call);
        
        /// <summary>
        /// Remove a non-persistent listener from the UnityEvent. If you have added the same listener multiple times, this method will remove all occurrences of it.
        /// </summary>
        /// <param name="call"></param>
        public void RemoveListener(UnityAction<T0> call) => unityEvent.RemoveListener(call);
        
        /// <summary>
        /// Remove all non-persistent (i.e. created from script) listeners from the event.
        /// </summary>
        public override void RemoveAllListeners() => unityEvent.RemoveAllListeners();
        
        /// <summary>
        /// Get the number of registered persistent listeners.
        /// </summary>
        /// <returns></returns>
        public override int GetPersistentEventCount() => unityEvent.GetPersistentEventCount();
        
        /// <summary>
        /// Get the target component of the listener at index index.
        /// </summary>
        /// <param name="index">Index of the listener to query.</param>
        /// <returns></returns>
        public override Object GetPersistentTarget(int index) => unityEvent.GetPersistentTarget(index);
        
        /// <summary>
        /// Get the target method name of the listener at index index.
        /// </summary>
        /// <param name="index">Index of the listener to query.</param>
        /// <returns></returns>
        public override string GetPersistentMethodName(int index) => unityEvent.GetPersistentMethodName(index);
        
        /// <summary>
        /// Returns the execution state of a persistent listener
        /// </summary>
        /// <param name="index">Index of the listener to query.</param>
        /// <returns>Execution state of the persistent listener.</returns>
        public override UnityEventCallState GetPersistentListenerState(int index) => unityEvent.GetPersistentListenerState(index);
        
    } //  class end
    
    #endregion
    
    //----------------------------------------------------------------------------------------------------
    
    #region double argument GgEvent class

    /// <summary>
    /// A double argument version of GGEvent.
    /// </summary>
    [Serializable]
    public class GgEvent<T0, T1> : GgEventBase
    {
        [SerializeField]
        [Tooltip("UnityEvent used as a base to allow subscribing to and invoking GGEvents")]
        private UnityEvent<T0, T1> unityEvent;
        
        /// <summary>
        /// Invoke all registered callbacks (runtime and persistent).
        /// </summary>
        public async void Invoke(T0 arg0, T1 args1)
        {
            UnityEvent<T0, T1> instanceEvent = unityEvent;
            if (verboseLogs && 0 < delay) { GgLogs.Log(logColor, null, GgLogType.Info, "{0} waiting for {1} seconds.", instanceName, delay); }
            
            TaskResultType waitResult = 0 < delay ? await GgTask.WaitForSeconds(delay, TokenSource) : TaskResultType.Complete;
            if (instanceEvent == null) { return; }
            switch (waitResult)
            {
                default:
                case TaskResultType.Failed:
                    if (verboseLogs) { GgLogs.Log(logColor, null, GgLogType.Info, "Unity Event '{0}' Failed.", instanceName); }
                    return;
                
                case TaskResultType.Timeout:
                    if (verboseLogs) { GgLogs.Log(logColor, null, GgLogType.Info, "Unity Event '{0}' Timeout.", instanceName); }
                    return;
                    
                case TaskResultType.Cancelled:
                    if (verboseLogs) { GgLogs.Log(logColor, null, GgLogType.Info, "Unity Event '{0}' Cancelled.", instanceName); }
                    return;
                    
                case TaskResultType.Complete:
                    if (verboseLogs) { GgLogs.Log(logColor, null, GgLogType.Info, "Unity Event '{0}' Invoked.", instanceName); }
                    instanceEvent?.Invoke(arg0, args1);
                    break;
            }
        }
        
        /// <summary>
        /// Returns the type information for all args.
        /// </summary>
        /// <returns></returns>
        public override Type[] GetArgTypes() => typeof(UnityEvent<T0, T1>).GetGenericArguments();
        
        /// <summary>
        /// Invoke all registered callbacks (runtime and persistent) using default args.
        /// </summary>
        internal override void InvokeEventWithDefaultArgs() => Invoke(default, default);
        
        /// <summary>
        /// Add a non-persistent listener to the UnityEvent.
        /// </summary>
        /// <param name="call"></param>
        public void AddListener(UnityAction<T0, T1> call) => unityEvent.AddListener(call);
        
        /// <summary>
        /// Remove a non-persistent listener from the UnityEvent. If you have added the same listener multiple times, this method will remove all occurrences of it.
        /// </summary>
        /// <param name="call"></param>
        public void RemoveListener(UnityAction<T0, T1> call) => unityEvent.RemoveListener(call);
        
        /// <summary>
        /// Remove all non-persistent (i.e. created from script) listeners from the event.
        /// </summary>
        public override void RemoveAllListeners() => unityEvent.RemoveAllListeners();
        
        /// <summary>
        /// Get the number of registered persistent listeners.
        /// </summary>
        /// <returns></returns>
        public override int GetPersistentEventCount() => unityEvent.GetPersistentEventCount();
        
        /// <summary>
        /// Get the target component of the listener at index index.
        /// </summary>
        /// <param name="index">Index of the listener to query.</param>
        /// <returns></returns>
        public override Object GetPersistentTarget(int index) => unityEvent.GetPersistentTarget(index);
        
        /// <summary>
        /// Get the target method name of the listener at index index.
        /// </summary>
        /// <param name="index">Index of the listener to query.</param>
        /// <returns></returns>
        public override string GetPersistentMethodName(int index) => unityEvent.GetPersistentMethodName(index);
        
        /// <summary>
        /// Returns the execution state of a persistent listener
        /// </summary>
        /// <param name="index">Index of the listener to query.</param>
        /// <returns>Execution state of the persistent listener.</returns>
        public override UnityEventCallState GetPersistentListenerState(int index) => unityEvent.GetPersistentListenerState(index);
        
    } //  class end
    
    #endregion
    
    //----------------------------------------------------------------------------------------------------
    
    #region triple argument GgEvent class

    /// <summary>
    /// A double argument version of GGEvent.
    /// </summary>
    [Serializable]
    public class GgEvent<T0, T1, T2> : GgEventBase
    {
        [SerializeField]
        [Tooltip("UnityEvent used as a base to allow subscribing to and invoking GGEvents")]
        private UnityEvent<T0, T1, T2> unityEvent;
        
        /// <summary>
        /// Invoke all registered callbacks (runtime and persistent).
        /// </summary>
        public async void Invoke(T0 arg0, T1 args1, T2 args2)
        {
            UnityEvent<T0, T1, T2> instanceEvent = unityEvent;
            if (verboseLogs && 0 < delay) { GgLogs.Log(logColor, null, GgLogType.Info, "{0} waiting for {1} seconds.", instanceName, delay); }
            
            TaskResultType waitResult = 0 < delay ? await GgTask.WaitForSeconds(delay, TokenSource) : TaskResultType.Complete;
            if (instanceEvent == null) { return; }
            switch (waitResult)
            {
                default:
                case TaskResultType.Failed:
                    if (verboseLogs) { GgLogs.Log(logColor, null, GgLogType.Info, "Unity Event '{0}' Failed.", instanceName); }
                    return;
                
                case TaskResultType.Timeout:
                    if (verboseLogs) { GgLogs.Log(logColor, null, GgLogType.Info, "Unity Event '{0}' Timeout.", instanceName); }
                    return;
                    
                case TaskResultType.Cancelled:
                    if (verboseLogs) { GgLogs.Log(logColor, null, GgLogType.Info, "Unity Event '{0}' Cancelled.", instanceName); }
                    return;
                    
                case TaskResultType.Complete:
                    if (verboseLogs) { GgLogs.Log(logColor, null, GgLogType.Info, "Unity Event '{0}' Invoked.", instanceName); }
                    instanceEvent?.Invoke(arg0, args1, args2);
                    break;
            }
        }
        
        /// <summary>
        /// Returns the type information for all args.
        /// </summary>
        /// <returns></returns>
        public override Type[] GetArgTypes() => typeof(UnityEvent<T0, T1, T2>).GetGenericArguments();
        
        /// <summary>
        /// Invoke all registered callbacks (runtime and persistent) using default args.
        /// </summary>
        internal override void InvokeEventWithDefaultArgs() => Invoke(default, default, default);
        
        /// <summary>
        /// Add a non-persistent listener to the UnityEvent.
        /// </summary>
        /// <param name="call"></param>
        public void AddListener(UnityAction<T0, T1, T2> call) => unityEvent.AddListener(call);
        
        /// <summary>
        /// Remove a non-persistent listener from the UnityEvent. If you have added the same listener multiple times, this method will remove all occurrences of it.
        /// </summary>
        /// <param name="call"></param>
        public void RemoveListener(UnityAction<T0, T1, T2> call) => unityEvent.RemoveListener(call);
        
        /// <summary>
        /// Remove all non-persistent (i.e. created from script) listeners from the event.
        /// </summary>
        public override void RemoveAllListeners() => unityEvent.RemoveAllListeners();
        
        /// <summary>
        /// Get the number of registered persistent listeners.
        /// </summary>
        /// <returns></returns>
        public override int GetPersistentEventCount() => unityEvent.GetPersistentEventCount();
        
        /// <summary>
        /// Get the target component of the listener at index index.
        /// </summary>
        /// <param name="index">Index of the listener to query.</param>
        /// <returns></returns>
        public override Object GetPersistentTarget(int index) => unityEvent.GetPersistentTarget(index);
        
        /// <summary>
        /// Get the target method name of the listener at index index.
        /// </summary>
        /// <param name="index">Index of the listener to query.</param>
        /// <returns></returns>
        public override string GetPersistentMethodName(int index) => unityEvent.GetPersistentMethodName(index);
        
        /// <summary>
        /// Returns the execution state of a persistent listener
        /// </summary>
        /// <param name="index">Index of the listener to query.</param>
        /// <returns>Execution state of the persistent listener.</returns>
        public override UnityEventCallState GetPersistentListenerState(int index) => unityEvent.GetPersistentListenerState(index);
        
    } //  class end
    
    #endregion
    
}
