using System;
using System.Collections.Generic;
using Vintagestory.API.Common;

namespace ClearView.Systems
{
    internal sealed class IgnoredMaterials
    {
        private readonly object materialLock = new object();
        private readonly HashSet<EnumBlockMaterial> materials = new HashSet<EnumBlockMaterial>();
        private readonly ClearViewSettings settings;

        public IgnoredMaterials(ClearViewSettings settings)
        {
            this.settings = settings;
            Load();
        }

        public bool IsIgnored(EnumBlockMaterial material)
        {
            lock (materialLock)
            {
                return materials.Contains(material);
            }
        }

        public bool IsIgnored(Block block)
        {
            return IsIgnored(block.BlockMaterial);
        }

        public void SetIgnored(EnumBlockMaterial material, bool ignored)
        {
            lock (materialLock)
            {
                if (ignored)
                {
                    materials.Add(material);
                }
                else
                {
                    materials.Remove(material);
                }
            }
        }

        public bool ToggleIgnored(EnumBlockMaterial material)
        {
            lock (materialLock)
            {
                if (materials.Contains(material))
                {
                    materials.Remove(material);
                    return false;
                }

                materials.Add(material);
                return true;
            }
        }

        public string Text()
        {
            string[] names;
            lock (materialLock)
            {
                if (materials.Count == 0)
                {
                    return "none";
                }

                names = new string[materials.Count];
                int index = 0;
                foreach (EnumBlockMaterial material in materials)
                {
                    names[index++] = material.ToString();
                }
            }

            Array.Sort(names, StringComparer.OrdinalIgnoreCase);
            return string.Join(", ", names);
        }

        public void WriteToSettings()
        {
            lock (materialLock)
            {
                string[] names = new string[materials.Count];
                int index = 0;
                foreach (EnumBlockMaterial material in materials)
                {
                    names[index++] = material.ToString();
                }

                Array.Sort(names, StringComparer.OrdinalIgnoreCase);
                settings.IgnoredMaterials = names;
            }
        }

        private void Load()
        {
            // store names instead of enum numbers so the config stays readable
            lock (materialLock)
            {
                materials.Clear();
                if (settings.IgnoredMaterials == null)
                {
                    settings.IgnoredMaterials = new string[0];
                    return;
                }

                for (int i = 0; i < settings.IgnoredMaterials.Length; i++)
                {
                    if (Enum.TryParse(settings.IgnoredMaterials[i], true, out EnumBlockMaterial material) && Enum.IsDefined(typeof(EnumBlockMaterial), material))
                    {
                        materials.Add(material);
                    }
                }
            }
        }
    }
}
