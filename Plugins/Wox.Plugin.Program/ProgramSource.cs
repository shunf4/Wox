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
        }

        public override bool Equals(object obj)
        {
            return obj is ProgramSource source &&
                   Location == source.Location &&
                   OverrideName == source.OverrideName &&
                   ExtraDesc == source.ExtraDesc &&
                   (ExtraExtensionList == source.ExtraExtensionList ||
                    (ExtraExtensionList != null && source.ExtraExtensionList != null &&
                     ExtraExtensionList.SequenceEqual(source.ExtraExtensionList))) &&
                   IsDirSelf == source.IsDirSelf &&
                   SearchDepthLimitOptional == source.SearchDepthLimitOptional &&
                   SearchOption == source.SearchOption &&
                   ShouldShowDirAsEntry == source.ShouldShowDirAsEntry;
        }

        public override int GetHashCode()
        {
            int hashCode = 1761937746;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Location);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(OverrideName);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(ExtraDesc);
            if (ExtraExtensionList != null)
            {
                foreach (var ext in ExtraExtensionList)
                {
                    hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(ext);
                }
            }
            else
            {
                hashCode = hashCode * -1521134295 + 0;
            }
            hashCode = hashCode * -1521134295 + IsDirSelf.GetHashCode();
            hashCode = hashCode * -1521134295 + SearchDepthLimitOptional.GetHashCode();
            hashCode = hashCode * -1521134295 + SearchOption.GetHashCode();
            hashCode = hashCode * -1521134295 + ShouldShowDirAsEntry.GetHashCode();
            return hashCode;
        }
    }
}
