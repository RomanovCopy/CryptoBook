using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.DTO
{
    public class MenuFileItem:MenuItemBase
    {
        public MenuFileItem(ICommandService commandService) : base(commandService)
        {
            IsEnabled = true;
            Initialize();
            HasChildren = Children.Count > 0;
        }

        protected override void Initialize()
        {
            Name = "File";
            List<(CommandKey, string)> keys = new(){
                (CommandKey.menuFile_NewFile, "NewFile"),
                (CommandKey.menuFile_OpenFile, "OpenFile"),
                (CommandKey.menuFile_SaveFile, "SaveFile"),
                (CommandKey.menuFile_SaveAsFile, "SaveAsFile"),
                (CommandKey.menuFile_FileOverview, "FileOverview"),
                (CommandKey.menuFile_OpenDirectory, "OpenDirectory"),
                (CommandKey.menuFile_CloseFile, "CloseFile"),
                (CommandKey.menuFile_UpdateFile, "UpdateFile"),
                (CommandKey.menuFile_WorkingDirectorySynchronization, "WorkingDirectorySynchronization"),
            };
            foreach(var (key, displayName) in keys)
            {
                var item = this.CreateItem(displayName, key);
                if(item != null)
                    Children.Add(item);
            }

        }

        private MenuItem CreateItem(string name, CommandKey commandKey)
        {
            return new MenuItem(commandService)
            {
                Name = "   " + name,
                IsEnabled = true,
                Command = commandService.GetCommand(commandKey) ?? throw new NullReferenceException($"ICommand {commandKey} not defined"),
            };

        }




    }
}
