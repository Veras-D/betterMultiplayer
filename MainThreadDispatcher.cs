using System;
using System.Collections.Generic;
using UnityEngine;

namespace BetterMultiplayer
{
    public class MainThreadDispatcher : MonoBehaviour
    {
        private static readonly Queue<Action> actionQueue = new Queue<Action>();
        private static readonly object queueLock = new object();

        public static void Enqueue(Action action)
        {
            lock (queueLock)
            {
                actionQueue.Enqueue(action);
            }
        }

        void Update()
        {
            lock (queueLock)
            {
                while (actionQueue.Count > 0)
                {
                    try
                    {
                        actionQueue.Dequeue()();
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
}
