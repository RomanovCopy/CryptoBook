# Bookmarks, notes and internal links

A bookmark inserts its own visible text from the entered name into an ordinary document. Save as XamlPackage or a protected CryptoBook document to preserve bookmark text, positions, names, notes and internal links when reopening. Bookmarks lost during saving in earlier versions cannot be restored automatically.

## Steps

1. Clear the text selection, position the caret and expand **Bookmarks** above the document. Enter a name and click **Add at caret position**: the name is inserted as the new bookmark's own text. Creation is unavailable while text is selected; a bookmark cannot be created from selected text.
2. Select a bookmark. **Go to selected bookmark**, **Previous bookmark** and **Next bookmark** center the bookmark line vertically in the editor viewport and briefly highlight it. Scrolling is limited by the document edges. Adjacent navigation wraps around the document; internal links also center and highlight their bookmark destinations.
3. Enter a new name and click **Rename selected bookmark** to rename it. To insert a link, clear the text selection, move the caret elsewhere, enter the link's own text in the caption field and click **Insert link to selected bookmark**. The caption is inserted into the document as new text. Insertion is unavailable while text is selected or the caption is empty; a bookmark link cannot be created from selected text.
4. Open **Open bookmark manager**, select an entry and edit its note. **Refresh index from document** rereads anchors in the current document.

In paged preview, these commands and internal links open the page containing the bookmark while keeping preview mode active, and highlight the destination line for 2.5 seconds. The highlight does not change document formatting. Creating bookmarks and inserting links are only available in the editor, including in the separate manager; the entered name and link text are retained when switching modes. Internal links and adjacent bookmark navigation update the selected entry in the Bookmarks menu in both modes.

![Bookmark panel with naming, navigation and link controls](images/S15-a.png "Bookmark panel with naming, navigation and link controls.")

![Bookmark manager with an editable practice note](images/S15-b.png "Bookmark manager with an editable practice note.")

## Check, undo and limitations

Check the name and destination. **Delete selected bookmark** deletes the entire bookmark, all its text and all links to it, including their captions. This also applies to bookmarks in older documents that were created from selected text. Bookmarks nested inside deleted text and links to them are also deleted; other bookmarks and external links outside the deleted text are preserved. Ctrl+Z undoes the entire deletion in one action. Correct an empty or duplicate name according to the panel message. Closing the manager does not cancel changes already made; save the document explicitly. Refresh the index if moving or deleting text removes a bookmark. TXT, XAML and RTF do not guarantee preservation of anchors and notes.

## Related topics

- [Web links and display text](help:edit.links)
- [The document structure tree](help:content.structure)
