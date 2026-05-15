using System;
using System.Collections.Generic;
using System.Linq;
using AcidemaQuirkMod.Core;

namespace AcidemaQuirkMod.Core
{
    internal static class WorldBoxApi
    {
        public static void LoadTraitJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            dynamic traits = AssetManager.traits;
            traits.loadFromJson(json);
        }

        public static object? GetTrait(string traitId)
        {
            if (string.IsNullOrEmpty(traitId)) return null;
            return AssetManager.traits.get(traitId);
        }

        public static bool TryGetActorId(object actor, out string unitId)
        {
            unitId = null!;
            if (actor == null) return false;
            unitId = GetMember<string>(actor, "data", "id");
            return !string.IsNullOrEmpty(unitId);
        }

        public static string? GetActorId(object actor)
            => GetMember<string>(actor, "data", "id");

        public static string? GetActorKillActionId(object actor)
            => GetMember<string>(actor, "data", "kill_action", "id");

        public static float GetActorHealth(object actor)
            => GetMember<float>(actor, "data", "health");

        public static float GetActorMaxHealth(object actor)
        {
            var result = DynamicBridge.CallInstanceMethod(actor, "getMaxHealth");
            return result is float f ? f : 0f;
        }

        public static float GetActorArmor(object actor)
        {
            return GetMember<float>(actor, "data", "stats", "armor");
        }

        public static void SetActorArmor(object actor, float value)
        {
            if (actor == null) return;
            var data = DynamicBridge.GetMemberValue(actor, "data");
            if (data == null) return;
            var stats = DynamicBridge.GetMemberValue(data, "stats");
            if (stats == null) return;
            var type = stats.GetType();
            var field = type.GetField("armor", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (field != null) field.SetValue(stats, value);
            else
            {
                var prop = type.GetProperty("armor", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                prop?.SetValue(stats, value);
            }
        }

        public static bool IsActorAlive(object actor)
        {
            if (actor == null) return false;
            var result = DynamicBridge.CallInstanceMethod(actor, "isAlive");
            return result is bool b && b;
        }

        public static bool ActorHasTrait(object actor, string traitId)
        {
            if (actor == null || string.IsNullOrEmpty(traitId)) return false;
            var result = DynamicBridge.CallInstanceMethod(actor, "hasTrait", traitId);
            return result is bool b && b;
        }

        public static void AddActorTrait(object actor, object trait)
        {
            if (actor == null || trait == null) return;
            DynamicBridge.CallInstanceMethod(actor, "addActorTrait", trait);
        }

        public static object? GetActorCurrentTile(object actor)
            => GetMember<object>(actor, "currentTile");

        public static IEnumerable<dynamic> GetTilesAround(object centerTile, int radius)
        {
            if (centerTile == null) return Enumerable.Empty<dynamic>();
            dynamic world = GetWorld();
            if (world == null) return Enumerable.Empty<dynamic>();

            var tiles = DynamicBridge.CallInstanceMethod(world, "getTilesAround", centerTile, radius);
            return tiles as IEnumerable<dynamic> ?? Enumerable.Empty<dynamic>();
        }

        public static dynamic GetActorFromTile(object tile)
        {
            if (tile == null) return null;
            dynamic world = GetWorld();
            return world?.getActorFromTile(tile);
        }

        public static IEnumerable<dynamic> GetWorldActors()
        {
            dynamic world = GetWorld();
            if (world == null) yield break;
            foreach (var actor in world.units)
                yield return actor;
        }

        public static object CallEffectsLibrary(string method, params object[] args)
        {
            return DynamicBridge.CallStaticMethod("EffectsLibrary", method, args);
        }

        public static object CallMapObjectHelper(string method, params object[] args)
        {
            return DynamicBridge.CallStaticMethod("MapObjectHelper", method, args);
        }

        private static dynamic GetWorld()
        {
            try
            {
                return (dynamic)World.world;
            }
            catch
            {
                return null;
            }
        }

        private static T GetMember<T>(object target, params string[] path)
        {
            if (target == null || path == null || path.Length == 0) return default;
            object current = target;
            foreach (var member in path)
            {
                if (current == null) return default;
                current = DynamicBridge.GetMemberValue(current, member);
            }
            return current is T value ? value : default;
        }
    }
}
