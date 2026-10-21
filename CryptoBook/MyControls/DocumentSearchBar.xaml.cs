namespace CryptoBook.MyControls
{
    public partial class DocumentSearchBar: System.Windows.Controls.UserControl
    {
        public DocumentSearchBar() => InitializeComponent();

        public System.Windows.Controls.TextBox QueryBox => QueryInput;
    }
}
