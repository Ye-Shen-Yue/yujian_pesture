using System.Collections.Generic;

namespace YuJian.Formation
{
    /// <summary>
    /// 阵法注册表 - 管理所有可用阵法定义
    /// </summary>
    public static class FormationLibrary
    {
        private static readonly Dictionary<string, FormationDefinition> _registry = new();

        /// <summary>注册阵法</summary>
        public static void Register(FormationDefinition def)
        {
            if (def != null && !string.IsNullOrEmpty(def.FormationName))
                _registry[def.FormationName] = def;
        }

        /// <summary>按名称获取阵法</summary>
        public static FormationDefinition Get(string name)
        {
            return _registry.TryGetValue(name, out var def) ? def : null;
        }

        /// <summary>获取所有已注册阵法</summary>
        public static IEnumerable<FormationDefinition> GetAll()
        {
            return _registry.Values;
        }

        /// <summary>初始化内置阵法</summary>
        public static void InitializeBuiltIn()
        {
            Register(FormationDefinition.CreateLiangYi());
            Register(FormationDefinition.CreateQiXing());
            Register(FormationDefinition.CreateZhuXian());
            Register(FormationDefinition.CreateTianGang());
        }
    }
}
