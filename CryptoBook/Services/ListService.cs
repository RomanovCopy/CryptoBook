using CryptoBook.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;

namespace CryptoBook.Services
{
    public class ListService: IListService
    {
        private readonly IDocumentSelection _sel;
        private readonly IEditTransaction _tx;

        public bool CanToggle
        {
            get
            {
                var paras = _sel.GetSelectedParagraphsOrCurrent();
                bool a=paras != null && paras.Count > 0;
                return a;
            }
        }

        public bool CanClear
        {
            get
            {
                var paras = _sel.GetSelectedParagraphsOrCurrent();
                if(paras.Count == 0)
                    return false;

                // хотя бы один параграф внутри списка
                return paras.Any(p => FindAncestor<List>(p) != null);
            }
        }

        public ListService(IDocumentSelection selection, IEditTransaction tx)
        {
            _sel = selection ?? throw new ArgumentNullException(nameof(selection));
            _tx = tx ?? throw new ArgumentNullException(nameof(tx));
        }

        public void ToggleBulleted() => ToggleCore(TextMarkerStyle.Disc, 1);
        public void ToggleNumbered(int startIndex = 1) => ToggleCore(TextMarkerStyle.Decimal, startIndex);

        public void ClearLists()
        {
            using(_tx.Begin())
            {
                var paras = _sel.GetSelectedParagraphsOrCurrent();
                if(paras.Count == 0)
                    return;
                UnwrapParagraphsFromLists(paras);
            }
        }

        // -------- core --------

        private void ToggleCore(TextMarkerStyle marker, int startIndex)
        {
            using(_tx.Begin())
            {
                var paras = _sel.GetSelectedParagraphsOrCurrent();
                if(paras.Count == 0)
                    return;

                if(AllInSameListWithMarker(paras, marker))
                {
                    UnwrapParagraphsFromLists(paras);
                    return;
                }

                // Нормализуем: убираем любые списки из выбранных параграфов
                UnwrapParagraphsFromLists(paras);

                // Группируем по владельцу блоков (FlowDocument/Section)
                foreach(var g in paras.GroupBy(GetBlocksOwner))
                {
                    var owner = g.Key;
                    if(owner is null)
                        continue;
                    var ordered = g.ToList();
                    // вставляем новый List перед первым из группы
                    var first = ordered.First();
                    var list = new List { MarkerStyle = marker, StartIndex = startIndex };
                    InsertBefore(owner, first, list);

                    // переносим параграфы в ListItem'ы
                    foreach(var p in ordered)
                    {
                        RemoveFromOwner(p);
                        var li = new ListItem();
                        li.Blocks.Add(p);
                        list.ListItems.Add(li);
                    }

                    EnsureFirstItemHasContent(list);
                }
            }
        }

        // -------- helpers: unwrap/detect --------

        private void UnwrapParagraphsFromLists(IReadOnlyList<Paragraph> paras)
        {
            if(paras.Count == 0)
                return;

            // группируем по исходному List
            var byList = paras
                .Select(p => new { Para = p, List = FindAncestor<List>(p) })
                .Where(x => x.List != null)
                .GroupBy(x => x.List!);

            foreach(var grp in byList)
            {
                var list = grp.Key;
                var owner = GetBlocksOwner(list);
                if(owner is null)
                    continue;

                // переносим только нужные параграфы наружу, сохраняя порядок
                foreach(var li in list.ListItems.ToList())
                {
                    foreach(var b in li.Blocks.ToList())
                    {
                        if(b is Paragraph p && paras.Contains(p))
                        {
                            li.Blocks.Remove(p);
                            InsertBefore(owner, list, p);
                        } else if(!(b is Paragraph))
                        {
                            // на всякий случай переносим нестандартные блоки
                            li.Blocks.Remove(b);
                            InsertBefore(owner, list, b);
                        }
                    }
                }

                // если список опустел — удалить
                var hasContent = list.ListItems.Cast<ListItem>().Any(li => li.Blocks.Count > 0);
                if(!hasContent)
                    RemoveFromOwner(list);
            }
        }

        private bool AllInSameListWithMarker(IReadOnlyList<Paragraph> paras, TextMarkerStyle marker)
        {
            if(paras.Count == 0)
                return false;

            List commonList = null;
            foreach(var p in paras)
            {
                var list = FindAncestor<List>(p);
                if(list == null)
                    return false;

                if(commonList == null)
                    commonList = list;
                if(!ReferenceEquals(commonList, list))
                    return false;
            }

            return commonList != null && commonList.MarkerStyle == marker;
        }

        // -------- helpers: structure ops --------

        private BlockCollection GetBlocksOwner(Block b)
        {
            if(b.Parent is FlowDocument d)
                return d.Blocks;
            if(b.Parent is Section s)
                return s.Blocks;
            if(b.Parent is ListItem li)
                return li.Blocks;
            return null;
        }

        private void InsertBefore(BlockCollection owner, Block before, Block toInsert)
        {
            if(owner == null || before == null || toInsert == null)
                return;
            if(owner.Contains(toInsert))
                return;

            // В .NET/WPF у BlockCollection есть InsertBefore/InsertAfter
            owner.InsertBefore(before, toInsert);
        }

        private void RemoveFromOwner(Block b)
        {
            var owner = GetBlocksOwner(b);
            owner?.Remove(b);
        }

        private T? FindAncestor<T>(DependencyObject d) where T : DependencyObject
        {
            var cur = d;
            while(cur != null && cur is not T)
                cur = LogicalTreeHelper.GetParent(cur);
            return cur as T;
        }

        private static void EnsureFirstItemHasContent(List list)
        {
            var firstItem = list.ListItems.FirstOrDefault();
            if(firstItem == null)
            {
                var li = new ListItem();
                var p = new Paragraph(new Run());
                li.Blocks.Add(p);
                list.ListItems.Add(li);
                return;
            }

            var firstPara = firstItem.Blocks.FirstBlock as Paragraph;
            if(firstPara == null)
            {
                firstPara = new Paragraph();
                firstItem.Blocks.InsertBefore(firstItem.Blocks.FirstBlock, firstPara);
            }

            if(firstPara.Inlines.FirstInline == null)
                firstPara.Inlines.Add(new Run()); // пустой Run, чтобы каретка могла «встать»
        }


    }
}
