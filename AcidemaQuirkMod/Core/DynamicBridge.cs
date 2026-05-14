using System;
using System.Linq;
using System.Reflection;

namespace AcidemaQuirkMod.Core
{
    internal static class DynamicBridge
    {
        public static object GetMemberValue(object target, string member)
        {
            if (target == null || string.IsNullOrEmpty(member)) return null;
            var type = target.GetType();
            var prop = type.GetProperty(member, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null) return prop.GetValue(target);
            var field = type.GetField(member, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null) return field.GetValue(target);
            return null;
        }

        public static object CallInstanceMethod(object target, string methodName, params object[] args)
        {
            if (target == null || string.IsNullOrEmpty(methodName)) return null;
            var type = target.GetType();
            var method = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m => m.Name == methodName && ParametersMatch(m.GetParameters(), args));
            return method?.Invoke(target, args);
        }

        public static object CallStaticMethod(string typeName, string methodName, params object[] args)
        {
            if (string.IsNullOrEmpty(typeName) || string.IsNullOrEmpty(methodName)) return null;
            var type = FindType(typeName);
            if (type == null) return null;
            var method = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m => m.Name == methodName && ParametersMatch(m.GetParameters(), args));
            return method?.Invoke(null, args);
        }

        private static Type FindType(string typeName)
        {
            var type = Type.GetType(typeName);
            if (type != null) return type;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = asm.GetType(typeName);
                if (type != null) return type;
            }
            return null;
        }

        private static bool ParametersMatch(ParameterInfo[] parameters, object[] args)
        {
            if (parameters.Length != args.Length) return false;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (args[i] == null) continue;
                if (!parameters[i].ParameterType.IsAssignableFrom(args[i].GetType()))
                    return false;
            }
            return true;
        }
    }
}
