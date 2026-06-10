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
                var sel = _sel.GetSelectedParagraphsOrCurrent();
                return sel.Any(p => !IsParagraphEmptyOrWhitespace(p));
            }
        }

        public bool CanClear
        {
            get
            {
                var sel = _sel.GetSelectedParagraphsOrCurrent();
                // можно чистить, если среди НЕпустых есть те, что находятся внутри списка
                return sel.Any(p => !IsParagraphEmptyOrWhitespace(p) && FindAncestor<List>(p) != null);
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
                var selected = _sel.GetSelectedParagraphsOrCurrent();
                if(selected.Count == 0)
                    return;

                // Вычисляем крайние блоки выделения
                var firstBlock = selected.First();    // гарантированно Paragraph
                var lastBlock = selected.Last();

                // Нормализуем: снять любые списки с непустых параграфов в выделении
                var nonEmpty = selected.Where(p => !IsParagraphEmptyOrWhitespace(p)).ToList();
                if(nonEmpty.Count == 0)
                    return; // всё пусто — нечего делать

                if(AllInSameListWithMarkerIgnoringEmpty(selected, marker))
                {
                    UnwrapParagraphsFromLists(nonEmpty);
                    return; // toggle off
                }

                //UnwrapParagraphsFromLists(nonEmpty);


                // Пробег от первого до последнего по указателю конца последнего
                var endAnchor = lastBlock.ElementEnd;           // граница, до которой идём

                // Владелец блока (FlowDocument/Section/ListItem)
                var owner = GetBlocksOwner(firstBlock);
                if(owner is null)
                    return;

                // 1) создаём ЕДИНЫЙ список перед первым блоком выделения
                var list = new List { MarkerStyle = marker, StartIndex = startIndex };
                InsertBefore(owner, firstBlock, list);

                // 2) идём по блокам в рамках ЭТОГО ЖЕ owner, от firstBlock до lastBlock ВКЛЮЧИТЕЛЬНО
                for(Block current = firstBlock; current != null;)
                {
                    var next = GetNextSiblingInOwner(owner, current); // берём next ДО возможного перемещения

                    if(current is Paragraph p && !IsParagraphEmptyOrWhitespace(p))
                    {
                        RemoveFromOwner(p);
                        var li = new ListItem();
                        li.Blocks.Add(p);
                        list.ListItems.Add(li);
                    }
                    // пустые параграфы пропускаем (оставляем на месте)

                    if(ReferenceEquals(current, lastBlock))
                        break; // включили lastBlock — выходим
                    current = next;
                }

                // если ничего не перенесли — удаляем пустой список
                if(list.ListItems.Count == 0)
                {
                    RemoveFromOwner(list);
                    return;
                }

                EnsureFirstItemHasContent(list);
                // MergeAdjacentLists(owner); // опционально

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

        // -------- helpers: structure ops --------

        private BlockCollection? GetBlocksOwner(Block b)
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

        private void EnsureFirstItemHasContent(List list)
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

        private bool IsParagraphEmptyOrWhitespace(Paragraph p)
        {
            if(p == null)
                return true;
            var tr = new TextRange(p.ContentStart, p.ContentEnd);
            // В WF/WPF пустой параграф обычно даёт "\r\n" — это считается пробелами
            return string.IsNullOrWhiteSpace(tr?.Text);
        }

        private bool AllInSameListWithMarkerIgnoringEmpty(
            IReadOnlyList<Paragraph> selected, TextMarkerStyle marker)
        {
            var nonEmpty = selected.Where(p => !IsParagraphEmptyOrWhitespace(p)).ToList();
            if(nonEmpty.Count == 0)
                return false;

            List common = null;
            foreach(var p in nonEmpty)
            {
                var list = FindAncestor<List>(p);
                if(list == null)
                    return false;
                if(common == null)
                    common = list;
                if(!ReferenceEquals(common, list))
                    return false;
            }
            return common != null && common.MarkerStyle == marker;
        }

        private Block? GetNextSiblingInOwner(BlockCollection owner, Block current)
        {
            // Проходим owner последовательно и возвращаем блок, следующий за current
            Block prev = null;
            foreach(var b in owner)
            {
                if(ReferenceEquals(prev, current))
                    return b;
                prev = b;
            }
            return null; // текущий был последним
        }





    }
}
