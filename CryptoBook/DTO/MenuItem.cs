using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.DTO
{
    public class MenuItem:MenuItemBase
    {
        public MenuItem(ICommandService commandService) : base(commandService)
        {
        }

        protected override void Initialize()
        {
            //base.Initialize();
        }
    }
}
