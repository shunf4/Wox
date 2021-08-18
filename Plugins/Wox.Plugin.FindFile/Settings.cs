using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Wox.Infrastructure.Storage;

namespace Wox.Plugin.FindFile
{
    public class Settings : BaseModel
    {
        public List<ContextMenu> FindFileContextMenu = new List<ContextMenu>()
        {
            new ContextMenu()
            {
                Name = "Open Containing Folder",
                Command = "explorer.exe",
                Argument = " /select \"{path}\""
            }
        };

        public List<IncludedFolder> IncludedFolders = new List<IncludedFolder>()
        {

        };
    }

    public class ContextMenu
    {
        public string Name { get; set; }

        public string Command { get; set; }

        public string Argument { get; set; }
    }

    public class IncludedFolder
    {
        public bool IncludeHidden { get; set; }

        public int MaxDepth { get; set; }

        public string Path { get; set; }
    }

}
