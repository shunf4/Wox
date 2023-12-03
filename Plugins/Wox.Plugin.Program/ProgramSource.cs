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
                }
                return result;
            }
        }

        public void CopyTo(ProgramSource t)
        {
            t.Location = Location;
            t.SearchOption = SearchOption;
            t.ShouldShowDirAsEntry = ShouldShowDirAsEntry;
        }

        public override bool Equals(object obj)
        {
            return obj is ProgramSource source &&
                   Location == source.Location &&
                   SearchOption == source.SearchOption &&
                   ShouldShowDirAsEntry == source.ShouldShowDirAsEntry;
        }

        public override int GetHashCode()
        {
            int hashCode = -1118069328;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Location);
            hashCode = hashCode * -1521134295 + SearchOption.GetHashCode();
            hashCode = hashCode * -1521134295 + ShouldShowDirAsEntry.GetHashCode();
            return hashCode;
        }
    }
}
