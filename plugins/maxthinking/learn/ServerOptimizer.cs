using System;
using System.Collections;
using System.Collections.Generic;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("ServerOptimizer", "ZombiePVE", "1.0.1")]
    [Description("Reduces lag spikes through various optimizations for high-pop servers")]
    public class ServerOptimizer : RustPlugin
    {
        private Configuration config;
        private Timer gcTimer;
        private Timer cleanupTimer;

        #region Configuration

        private class Configuration
        {
            public bool EnableGCOptimization { get; set; } = true;
            public float GCInterval { get; set; } = 300f;
            public bool EnableEntityCleanup { get; set; } = true;
            public float CleanupInterval { get; set; } = 600f;
            public bool EnableNetworkOptimization { get; set; } = true;
            public bool EnableAnimalOptimization { get; set; } = true;
            public bool EnablePhysicsOptimization { get; set; } = true;
            public bool LogOptimizations { get; set; } = false;
            public int MaxCorpsesPerPlayer { get; set; } = 4;
            public float CorpseLifetime { get; set; } = 900f; // 15 minutes
            public float DroppedItemLifetime { get; set; } = 900f; // 15 minutes
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
            SaveConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();
            }
            catch { LoadDefaultConfig(); }
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion

        #region Hooks

        private void OnServerInitialized()
        {
            ApplyOptimizations();
            StartTimers();
            Puts("Server optimizations applied!");
        }

        private void Unload()
        {
            gcTimer?.Destroy();
            cleanupTimer?.Destroy();
        }

        #endregion

        #region Optimizations

        private void ApplyOptimizations()
        {
            // Physics optimizations
            if (config.EnablePhysicsOptimization)
            {
                Physics.autoSyncTransforms = false;
                
                if (config.LogOptimizations)
                    Puts("Physics optimizations applied");
            }

            // Animal/AI optimizations
            if (config.EnableAnimalOptimization)
            {
                try
                {
                    ConVar.AI.think = true;
                    ConVar.AI.move = true;
                    
                    if (config.LogOptimizations)
                        Puts("AI optimizations applied");
                }
                catch { }
            }

            // Corpse settings
            try
            {
                ConVar.Server.corpsedespawn = config.CorpseLifetime;
                if (config.LogOptimizations)
                    Puts($"Corpse despawn set to {config.CorpseLifetime}s");
            }
            catch { }
        }

        private void StartTimers()
        {
            // Garbage Collection timer
            if (config.EnableGCOptimization)
            {
                gcTimer = timer.Every(config.GCInterval, () =>
                {
                    ServerMgr.Instance.StartCoroutine(SmoothGC());
                });
            }

            // Entity cleanup timer
            if (config.EnableEntityCleanup)
            {
                cleanupTimer = timer.Every(config.CleanupInterval, () =>
                {
                    ServerMgr.Instance.StartCoroutine(CleanupEntities());
                });
            }
        }

        private IEnumerator SmoothGC()
        {
            if (config.LogOptimizations)
                Puts("Running incremental garbage collection...");

            // Run GC in incremental mode to avoid spike
            GC.Collect(0, GCCollectionMode.Optimized, false);
            yield return new WaitForSeconds(0.5f);
            
            GC.Collect(1, GCCollectionMode.Optimized, false);
            yield return new WaitForSeconds(0.5f);
            
            // Only do full GC if memory is high
            var memoryUsed = GC.GetTotalMemory(false) / 1024 / 1024;
            if (memoryUsed > 4000) // Over 4GB
            {
                GC.Collect(2, GCCollectionMode.Optimized, false);
                yield return new WaitForSeconds(0.5f);
            }

            if (config.LogOptimizations)
                Puts($"GC complete. Memory: {memoryUsed}MB");
        }

        private IEnumerator CleanupEntities()
        {
            if (config.LogOptimizations)
                Puts("Running entity cleanup...");

            int cleaned = 0;

            // Clean up excess corpses per player
            var corpses = UnityEngine.Object.FindObjectsOfType<PlayerCorpse>();
            var corpsesByPlayer = new Dictionary<ulong, List<PlayerCorpse>>();

            foreach (var corpse in corpses)
            {
                if (corpse == null || corpse.IsDestroyed) continue;
                
                if (!corpsesByPlayer.ContainsKey(corpse.playerSteamID))
                    corpsesByPlayer[corpse.playerSteamID] = new List<PlayerCorpse>();
                
                corpsesByPlayer[corpse.playerSteamID].Add(corpse);
            }

            foreach (var kvp in corpsesByPlayer)
            {
                if (kvp.Value.Count > config.MaxCorpsesPerPlayer)
                {
                    // Sort by network ID (older = lower ID typically)
                    kvp.Value.Sort((a, b) => a.net.ID.Value.CompareTo(b.net.ID.Value));
                    
                    // Remove oldest corpses beyond the limit
                    for (int i = 0; i < kvp.Value.Count - config.MaxCorpsesPerPlayer; i++)
                    {
                        try
                        {
                            if (kvp.Value[i] != null && !kvp.Value[i].IsDestroyed)
                            {
                                kvp.Value[i].Kill();
                                cleaned++;
                            }
                        }
                        catch { }
                    }
                }
                
                yield return null;
            }

            // Clean up null entries in save list
            try
            {
                BaseEntity.saveList.RemoveWhere(x => x == null || x.IsDestroyed);
            }
            catch { }

            if (config.LogOptimizations)
                Puts($"Cleanup complete. Removed {cleaned} excess corpses.");
        }

        #endregion

        #region Commands

        [ConsoleCommand("optimize.status")]
        private void CmdStatus(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;

            var memory = GC.GetTotalMemory(false) / 1024 / 1024;
            var entities = BaseNetworkable.serverEntities.Count;
            var saveList = BaseEntity.saveList.Count;
            var players = BasePlayer.activePlayerList.Count;
            var sleepers = BasePlayer.sleepingPlayerList.Count;

            arg.ReplyWith($"=== Server Optimization Status ===\n" +
                         $"Memory Used: {memory}MB\n" +
                         $"Total Entities: {entities}\n" +
                         $"Saveable Entities: {saveList}\n" +
                         $"Active Players: {players}\n" +
                         $"Sleepers: {sleepers}\n" +
                         $"Max Corpses Per Player: {config.MaxCorpsesPerPlayer}\n" +
                         $"Corpse Lifetime: {config.CorpseLifetime}s");
        }

        [ConsoleCommand("optimize.gc")]
        private void CmdGC(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            
            ServerMgr.Instance.StartCoroutine(SmoothGC());
            arg.ReplyWith("Incremental GC started...");
        }

        [ConsoleCommand("optimize.cleanup")]
        private void CmdCleanup(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            
            ServerMgr.Instance.StartCoroutine(CleanupEntities());
            arg.ReplyWith("Entity cleanup started...");
        }

        [ConsoleCommand("optimize.memory")]
        private void CmdMemory(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            
            var before = GC.GetTotalMemory(false) / 1024 / 1024;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            var after = GC.GetTotalMemory(true) / 1024 / 1024;
            
            arg.ReplyWith($"Memory: {before}MB -> {after}MB (freed {before - after}MB)");
        }

        #endregion
    }
}
