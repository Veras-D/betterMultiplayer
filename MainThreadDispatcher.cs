using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterMultiplayer
{
    public class MainThreadDispatcher : MonoBehaviour
    {
        private static readonly Queue<Action> actionQueue = new Queue<Action>();
        private static readonly object queueLock = new object();

        private static readonly List<Action> executionList = new List<Action>();

        public static void Enqueue(Action action)
        {
            lock (queueLock)
            {
                actionQueue.Enqueue(action);
            }
        }

        void Update()
        {
            executionList.Clear();
            lock (queueLock)
            {
                while (actionQueue.Count > 0)
                {
                    executionList.Add(actionQueue.Dequeue());
                }
            }

            foreach (var action in executionList)
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    if (BetterMultiplayer.Instance != null)
                    {
                        BetterMultiplayer.Instance.LogError("Error executing main thread action: " + ex);
                    }
                }
            }
        }
    }
}
