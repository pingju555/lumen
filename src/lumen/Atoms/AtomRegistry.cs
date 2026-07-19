using System;
using System.Collections.Generic;

namespace Lumen.Atoms
{
    /// <summary>
    /// 原子运行时注册表：按 Type 分发工厂。P1 仅编译期注册；插件化动态加载留 v2。
    /// 详见 docs/project/phases/P1_渲染基座与画布/P1-03_Atom抽象与注册.md
    /// </summary>
    public static class AtomRegistry
    {
        private static readonly Dictionary<string, Func<Atom>> _reg =
            new(StringComparer.OrdinalIgnoreCase);

        public static void Register(string type, Func<Atom> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            _reg[type] = factory;
        }

        public static Atom Create(string type)
        {
            if (_reg.TryGetValue(type, out var f)) return f();
            throw new KeyNotFoundException($"Atom type not registered: {type}");
        }

        public static bool IsRegistered(string type) => _reg.ContainsKey(type);
    }
}
