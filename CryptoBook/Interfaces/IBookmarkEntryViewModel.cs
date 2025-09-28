using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IBookmarkEntryViewModel:IViewModel
    {
        public string Name { get; set; }
        public string Note { get; set; }
        public Uri? BookmarkUri{ get; }
        public string BookmarkUriString { get; }
    }
}
