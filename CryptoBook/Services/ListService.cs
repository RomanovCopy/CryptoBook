using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Services
{
    public class ListService:IListService
    {
        private readonly IRichTextBoxService service;

        public ListService(IRichTextBoxService service)
        {
            this.service = service;
        }

        public void ToggleBulleted()
        {
            throw new NotImplementedException();
        }

        public void ToggleNumbered(int startIndex = 1)
        {
            throw new NotImplementedException();
        }

        public void ClearLists()
        {
            throw new NotImplementedException();
        }

        public bool CanToggle => throw new NotImplementedException();
    }
}
