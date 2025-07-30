using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoBook.Interfaces
{
    public interface IImageService
    {
        IRichTextBoxService Service { get; }

        void InsertImage(string imagePath);
        void InsertImage(System.Drawing.Image image);
        void RemoveImage();
    }
}
