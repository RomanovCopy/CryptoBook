using CryptoBook.Infrastructure;
using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Models
{
    internal class BookmarksModel:ViewModelBase
    {
        private readonly IRichTextBoxService service;
        private readonly IBookmarkService bookmarkService;

        internal Dictionary<string, Uri> Bookmarks=>bookmarkService.Bookmarks;

        public BookmarksModel(IRichTextBoxService service)
        {
            this.service = service;
        }

    }
}
