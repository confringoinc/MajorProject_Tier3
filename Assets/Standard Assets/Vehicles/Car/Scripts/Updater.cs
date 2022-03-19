using System.Collections.Generic;
using UnityEngine;

namespace customInterface{
    public interface IUpdate{
        bool needsUpdate{ get; set; }
        void performUpdate(float time);

        bool needsFixedUpdate{ get; set; }
        void performFixedUpdate(float time);
    }

    public class Updater: MonoBehaviour{
        public static Updater Instance{
            get{
                if(instance == null){
                    instance = FindObjectOfType<Updater>();
                    // Debug.LogError("Instance set to Updater!");
                }
                // if(instance == null){
                //     Debug.LogError("Instance is null MF!");
                // }else{
                //     Debug.LogError("Instance is !null MF!");
                // }
                //Debug.LogError("Inside Get()" + instance);
                return instance;
            }
            set{
                // Debug.LogError("Inside Set()" + value);
                instance = value;
            }
        }
        private static Updater instance;


        private List<IUpdate> UpdateQueue = new List<IUpdate>();
        private List<IUpdate> FixedUpdateQueue = new List<IUpdate>();

        private List<IUpdate> UpdateAddQueue = new List<IUpdate>();
        private List<IUpdate> FixedUpdateAddQueue = new List<IUpdate>();

        private List<IUpdate> UpdateRemovalQueue = new List<IUpdate>();
        private List<IUpdate> FixedUpdateRemovalQueue = new List<IUpdate>();

        public enum UpdateType { Update, FixedUpdate }
        public void RegisterUpdate(IUpdate script, UpdateType updateType)
        {
            if (updateType == UpdateType.Update)
            {
                // Debug.LogError("UpdateType: " + updateType);
                UpdateAddQueue.Add(script);
            }
            else if (updateType == UpdateType.FixedUpdate)
            {
                // Debug.LogError("UpdateType: " + updateType);
                FixedUpdateAddQueue.Add(script);
            }
        }
        public void UnregisterUpdate(IUpdate script, UpdateType updateType)
        {
            if (updateType == UpdateType.Update)
            {
                UpdateRemovalQueue.Add(script);
            }
            else if (updateType == UpdateType.FixedUpdate)
            {
                FixedUpdateRemovalQueue.Add(script);
            }
        }

        void FixedUpdate()
        {
            if (FixedUpdateAddQueue.Count > 0)
            {
                for (int i = FixedUpdateAddQueue.Count - 1; i >= 0; i--)
                {
                    FixedUpdateQueue.Add(FixedUpdateAddQueue[i]);
                    FixedUpdateAddQueue.Remove(FixedUpdateAddQueue[i]);
                }
            }
            if (FixedUpdateRemovalQueue.Count > 0)
            {
                for (int i = FixedUpdateRemovalQueue.Count - 1; i >= 0; i--)
                {
                    FixedUpdateQueue.Remove(FixedUpdateRemovalQueue[i]);
                    FixedUpdateRemovalQueue.Remove(FixedUpdateRemovalQueue[i]);
                }
            }
            foreach (IUpdate queued in FixedUpdateQueue)
            {
                queued.performFixedUpdate(Time.deltaTime);
            }
        }

        void Update()
        {
            if (UpdateAddQueue.Count > 0)
            {
                for (int i = UpdateAddQueue.Count - 1; i >= 0; i--)
                {
                    UpdateQueue.Add(UpdateAddQueue[i]);
                    UpdateAddQueue.Remove(UpdateAddQueue[i]);
                }
            }
            if (UpdateRemovalQueue.Count > 0)
            {
                for (int i = UpdateRemovalQueue.Count - 1; i >= 0; i--)
                {
                    UpdateQueue.Remove(UpdateRemovalQueue[i]);
                    UpdateRemovalQueue.Remove(UpdateRemovalQueue[i]);
                }
            }
            foreach (IUpdate queued in UpdateQueue)
            {
                queued.performUpdate(Time.deltaTime);
            }
        }
        // void Awake()
        // {
        //     Instance = this;
        //     MonoBehaviour[] scripts = FindObjectsOfType<MonoBehaviour>();
        //     foreach (MonoBehaviour script in scripts)
        //     {
        //         if (script is IUpdate)
        //         {
        //             if ((script as IUpdate).needsUpdate)
        //             {
        //                 UpdateQueue.Add((IUpdate)script);
        //             }
        //             if ((script as IUpdate).needsFixedUpdate)
        //             {
        //                 FixedUpdateQueue.Add((IUpdate)script);
        //             }
        //         }
        //     }
        // }
    }
}