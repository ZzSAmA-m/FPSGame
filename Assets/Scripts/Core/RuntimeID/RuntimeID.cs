using System;
using System.Collections.Generic;

namespace Core
{
    /// <summary>为当前运行域中的每个名称分配唯一的整数 ID。</summary>
    public static class RuntimeID
    {
        private static readonly object SyncRoot = new object();
        private static readonly HashSet<string> Names = new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<int> UsedIds = new HashSet<int>();

        /// <summary>按字符串哈希分配 ID。重名时抛出异常，哈希冲突时顺延。</summary>
        public static int ToRuntimeID(string name)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            lock (SyncRoot)
            {
                if (!Names.Add(name))
                    throw new InvalidOperationException($"运行时 ID 名称重复：{name}");

                int id = name.GetHashCode();
                while (!UsedIds.Add(id))
                    id = unchecked(id + 1);

                return id;
            }
        }
    }
}
