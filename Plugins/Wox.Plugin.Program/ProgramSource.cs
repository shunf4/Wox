using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wox.Plugin.Program
{
    public class ProgramSource
    {
        public string Location { get; set; }
        public string OverrideName { get; set; }
        public string ExtraDesc { get; set; }
        public List<string> ExtraExtensionList { get; set; }
        public bool IsDirSelf { get; set; } = false;
        public bool IsShowRelPathAsName { get; set; } = false;

        public int? SearchDepthLimitOptional { get; set; }

        public SearchOption SearchOption { get; set; }

        public bool ShouldShowDirAsEntry { get; set; } = false;

        public string SettingEditSourceCode
        {
            get
            {
                string result = Location;
                if (ShouldShowDirAsEntry)
                {
                    result = "*" + result;
                }
                if (SearchOption == SearchOption.TopDirectoryOnly)
                {
                    result = "!" + result;
                } else
                {
                    if (SearchDepthLimitOptional != null)
                    {
                        if (SearchDepthLimitOptional == 3)
                        {
                            result = "!!!" + result;
                        } else if (SearchDepthLimitOptional == 2)
                        {
                            result = "!!" + result;
                        }
                        else if (SearchDepthLimitOptional == 1)
                        {
                            result = "!" + result;
                        }
                    }
                }
                if (OverrideName != null)
                {
                    result = result + "::name=" + OverrideName;
                }
                if (ExtraDesc != null)
                {
                    result = result + "::desc=" + ExtraDesc;
                }
                if (ExtraExtensionList != null && ExtraExtensionList.Count > 0)
                {
                    result = result + "::extraExts=" + String.Join(",", ExtraExtensionList);
                }
                if (IsDirSelf)
                {
                    result = result + "::dirSelf";
                }
                if (IsShowRelPathAsName)
                {
                    result = result + "::showRelPathAsName";
                }
                return result;
            }
        }

        public void CopyTo(ProgramSource t)
        {
            t.Location = Location;
            t.SearchOption = SearchOption;
            t.ShouldShowDirAsEntry = ShouldShowDirAsEntry;
            t.SearchDepthLimitOptional = SearchDepthLimitOptional;
            t.OverrideName = OverrideName;
            t.ExtraDesc = ExtraDesc;
            t.ExtraExtensionList = ExtraExtensionList == null ? null : new List<string>(ExtraExtensionList);
            t.IsDirSelf = IsDirSelf;
            t.IsShowRelPathAsName = IsShowRelPathAsName;
        }

        public bool ValueEquals(ProgramSource other)
        {
            return other != null &&
                   Location == other.Location &&
                   OverrideName == other.OverrideName &&
                   ExtraDesc == other.ExtraDesc &&
                   (ExtraExtensionList == other.ExtraExtensionList ||
                    (ExtraExtensionList != null && other.ExtraExtensionList != null &&
                     ExtraExtensionList.SequenceEqual(other.ExtraExtensionList))) &&
                   IsDirSelf == other.IsDirSelf &&
                   IsShowRelPathAsName == other.IsShowRelPathAsName &&
                   SearchDepthLimitOptional == other.SearchDepthLimitOptional &&
                   SearchOption == other.SearchOption &&
                   ShouldShowDirAsEntry == other.ShouldShowDirAsEntry;
        }

        private sealed class ValueEqualityComparer : IEqualityComparer<ProgramSource>
        {
            public bool Equals(ProgramSource x, ProgramSource y) => x == y || (x != null && x.ValueEquals(y));
            public int GetHashCode(ProgramSource obj)
            {
                unchecked
                {
                    int h = 17;
                    h = h * 31 + (obj.Location?.GetHashCode() ?? 0);
                    h = h * 31 + (obj.OverrideName?.GetHashCode() ?? 0);
                    h = h * 31 + (obj.ExtraDesc?.GetHashCode() ?? 0);
                    h = h * 31 + (obj.IsDirSelf ? 1 : 0);
                    h = h * 31 + (obj.IsShowRelPathAsName ? 1 : 0);
                    h = h * 31 + (obj.SearchDepthLimitOptional ?? 0);
                    h = h * 31 + obj.SearchOption.GetHashCode();
                    h = h * 31 + (obj.ShouldShowDirAsEntry ? 1 : 0);
                    if (obj.ExtraExtensionList != null)
                    {
                        foreach (var ext in obj.ExtraExtensionList)
                            h = h * 31 + (ext?.GetHashCode() ?? 0);
                    }
                    return h;
                }
            }
        }

        public static IEqualityComparer<ProgramSource> ByValueComparer { get; } = new ValueEqualityComparer();
    }
}
