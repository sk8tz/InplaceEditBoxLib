﻿namespace SolutionLib.ViewModels.Browser
{
    using InplaceEditBoxLib.Events;
    using SolutionLib.Interfaces;
    using SolutionLib.Models;
    using System;
    using System.Collections.ObjectModel;
    using System.Windows.Input;
    using System.Windows.Threading;

    /// <summary>
    /// A Solution root is the viewmodel that hosts all other solution related items.
    /// Even the SolutionRootItem that is part of the displayed collection is hosted in
    /// the collection below.
    /// </summary>
    internal class SolutionViewModel : Base.BaseViewModel, ISolution
    {
        #region fields
        private ISolutionRootItem _SolutionRootItem = null;
        private readonly ObservableCollection<ISolutionBaseItem> _Root = null;
        private ICommand _RenameCommand = null;
        private ICommand _StartRenameCommand;

        private ICommand _SelectionChangedCommand;
        private ISolutionBaseItem _SelectedItem;
        private ICommand _ItemAddCommand;
        private ICommand _ItemRemoveCommand;
        private ICommand _ItemClearCommand;
        #endregion fields

        #region constructors
        /// <summary>
        /// Class constructor
        /// </summary>
        public SolutionViewModel()
        {
            _Root = new ObservableCollection<ISolutionBaseItem>();
        }
        #endregion constructors

        #region properties
        /// <summary>
        /// Gets the root of the treeview. That is, there is only
        /// 1 item in the ObservableCollection and that item is the root.
        /// 
        /// The Children property of that one <see cref="ISolutionItem"/>
        /// represents the rest of the tree.
        /// </summary>
        public ObservableCollection<ISolutionBaseItem> Root
        {
            get
            {
                return _Root;
            }
        }

        /// <summary>
        /// Gets a command that adds a new item into the treeview.
        /// 
        /// Parameter is a Tuple with the <see cref="ISolutionItem"/> that is the
        /// parent of the to be creaed item and a <see cref="SolutionItemType"/>
        /// that is the type of the child that should be added here.
        /// </summary>
        public ICommand ItemAddCommand
        {
            get
            {
                if (_ItemAddCommand == null)
                    _ItemAddCommand = new Base.RelayCommand<object>(p =>
                    {
                        var tuple = p as Tuple<ISolutionItem, SolutionItemType>;

                        if (tuple == null)
                            return;

                        var parentItem = tuple.Item1;
                        var addType = tuple.Item2;

                        string nextChildItemName = parentItem.SuggestNextChildName(addType);

                        if (string.IsNullOrEmpty(nextChildItemName) == true)
                            return;

                        ISolutionBaseItem item = null;

                        item = parentItem.AddChild(nextChildItemName, addType);
                        parentItem.IsItemExpanded = true;
                        parentItem.SortChildren();

                        if (item != null)
                        {
                            // Request EditMode will only work if this is done with LOW priority
                            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, (Action)delegate
                            {
                                item.IsItemSelected = true;
                                item.RequestEditMode(InplaceEditBoxLib.Events.RequestEditEvent.StartEditMode);
                            });
                        }
                    },
                    ((p) =>
                    {
                        var tuple = p as Tuple<ISolutionItem, SolutionItemType>;

                        if (tuple == null)
                            return false;

                        var parentItem = tuple.Item1;
                        var addType = tuple.Item2;

                        switch (parentItem.ItemType)
                        {
                            // Folder and SolutionRoot should be able to contain anything
                            case SolutionItemType.Folder:
                            case SolutionItemType.SolutionRootItem:
                                return true;

                            // Files should not have any children of their own
                            case SolutionItemType.File:
                                return false;

                            // Projects can contain anything except for Projects
                            case SolutionItemType.Project:
                                if (addType == SolutionItemType.Project)
                                    return false;
                                else
                                    return true;

                            default:
                                throw new ArgumentOutOfRangeException(parentItem.ItemType.ToString());
                        }
                    }));

                return _ItemAddCommand;
            }
        }

        /// <summary>
        /// Gets a command that removes an item from the treeview.
        /// </summary>
        public ICommand ItemRemoveCommand
        {
            get
            {
                if (_ItemRemoveCommand == null)
                    _ItemRemoveCommand = new Base.RelayCommand<object>(p =>
                    {
                        var item = p as ISolutionBaseItem;

                        if (p == null)
                            return;

                        item.Parent.RemoveChild(item);
                    }, (p =>
                    {
                        var item = p as ISolutionBaseItem;

                        if (p == null)
                            return false;

                        // Lets disable removal of root since that does not
                        // seem to make a lot of sense here
                        if (item.Parent == null)
                            return false;

                        return true;
                    }));

                return _ItemRemoveCommand;
            }
        }

        /// <summary>
        /// Gets a command that removes all items below a given item.
        /// </summary>
        public ICommand ItemClearCommand
        {
            get
            {
                if (_ItemClearCommand == null)
                {
                    _ItemClearCommand = new Base.RelayCommand<object>(p =>
                    {
                        var item = p as ISolutionBaseItem;

                        if (item == null)
                            return;

                        item.RemoveAllChild();
                    });
                }

                return _ItemClearCommand;
            }
        }

        /// <summary>
        /// Starts the rename folder process by that renames the folder
        /// that is represented by this viewmodel.
        /// 
        /// This command implements an event that triggers the actual rename
        /// process in the connected view. The connected view in turn call a
        /// <see cref="RenameCommand"/> to actually perform the renaming in the
        /// data (unless user has cancelled in the meantime via ESC key).
        /// So, renaming realy has 3 parts:
        /// 
        /// 1) StartRenaming (can be triggered her or by the view itself)
        /// 2) Interaction in which the user interacts with the view to edit a string
        /// 3) RenameCommand -> perform renaming in data structure and update item collection
        /// </summary>
        public ICommand StartRenameCommand
        {
            get
            {
                if (_StartRenameCommand == null)
                    _StartRenameCommand = new Base.RelayCommand<object> (it =>
                    {
                        var item = it as ISolutionBaseItem;

                        if (item != null)
                        {
                            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, (Action)delegate
                            {
                                item.RequestEditMode(InplaceEditBoxLib.Events.RequestEditEvent.StartEditMode);
                            });
                         }
                    },
                    (it) =>
                    {
                        var item = it as ISolutionBaseItem;

                        if (item != null)
                        {
                            if (item.IsReadOnly == true)
                                return false;
                        }

                        return true;
                    });

                return _StartRenameCommand;
            }
        }

        /// <summary>
        /// Gets a command that Renames the item that is represented by this viewmodel.
        /// 
        /// This command should be called directly by the implementing view
        /// since the new name of the item is delivered as string with the
        /// item itself as second parameter via bound via RenameCommandParameter
        /// dependency property.
        /// </summary>
        public ICommand RenameCommand
        {
            get
            {
                if (_RenameCommand == null)
                {
                    _RenameCommand = new Base.RelayCommand<object>((p) =>
                    {
                        var tuple = p as Tuple<string, object>;

                        if (tuple != null)
                        {
                            var solutionItem = tuple.Item2 as ISolutionBaseItem;

                            if (tuple.Item1 != null && solutionItem != null)
                            {
                                string newName = tuple.Item1;

                                // Do we already know this item?
                                if (string.IsNullOrEmpty(newName) == true ||
                                  newName.Length < 1 || newName.Length > 254)
                                {
                                    solutionItem.RequestEditMode(RequestEditEvent.StartEditMode);
                                    solutionItem.ShowNotification("Invalid legth of name",
                                        "A name must be between 1 and 254 characters long.");
                                    return;
                                }

                                var parent = solutionItem.Parent;

                                if (parent != null)
                                {
                                    // Do we already know this item?
                                    var existingItem = parent.FindChild(newName);
                                    if (existingItem != null && existingItem != solutionItem)
                                    {
                                        solutionItem.RequestEditMode(RequestEditEvent.StartEditMode);
                                        solutionItem.ShowNotification("Item Already Exists",
                                            "An item with this name exists already. All names must be unique.");

                                        return;
                                    }

                                    parent.RenameChild(solutionItem, newName);

                                    // This parent selection + sort + child selection
                                    // scrolls the renamed item into view...
                                    parent.IsItemSelected = true;
                                    parent.IsItemExpanded = true;   // Ensure parent is expanded
                                    parent.SortChildren();
                                    solutionItem.IsItemSelected = true;
                                }
                                else
                                {
                                    // Is this a root item - it could then rename itself
                                    var solutionRootItem = tuple.Item2 as ISolutionRootItem;
                                    newName = tuple.Item1;

                                    if (solutionRootItem != null &&
                                        string.IsNullOrEmpty( newName ) == false)
                                    {
                                        solutionRootItem.RenameRootItem(newName);
                                    }
                                }
                            }
                        }
                    });
                }

                return _RenameCommand;
            }
        }

        public ICommand SelectionChangedCommand
        {
            get
            {
                if (_SelectionChangedCommand == null)
                {
                    _SelectionChangedCommand = new Base.RelayCommand<object>((p) =>
                    {
                        SelectedItem = p as ISolutionBaseItem;
                    });
                }

                return _SelectionChangedCommand;
            }
        }

        /// <summary>
        /// Gets the currently selected from the collection of tree items.
        /// </summary>
        public ISolutionBaseItem SelectedItem
        {
            get { return _SelectedItem; }

            private set
            {
                if (_SelectedItem != value)
                {
                    _SelectedItem = value;
                    NotifyPropertyChanged(() => SelectedItem);
                }
            }     
        }

        /// <summary>
        /// Renames the  displayed string in the <paramref name="solutionItem"/>
        /// as requested in <paramref name="newDisplayName"/>.
        /// </summary>
        /// <param name="solutionItem"></param>
        /// <param name="newDisplayName"></param>
        public void RenameItem(ISolutionBaseItem solutionItem, string newDisplayName)
        {
            solutionItem.SetDisplayName(newDisplayName);
        }
        #endregion properties

        #region methods
        /// <summary>
        /// Adds a solution root into the collection of solution items.
        /// 
        /// Be careful here (!) since the current root item (if any) is discarded
        /// along with all its children since the viewmodel does support only ONE root
        /// at all times.
        /// </summary>
        /// <param name="displayName"></param>
        /// <returns></returns>
        public ISolutionBaseItem AddSolutionRootItem(string displayName)
        {
            if (_SolutionRootItem != null)
            {
                _Root.Remove(_SolutionRootItem);
                _SolutionRootItem = null;
            }

            _SolutionRootItem = new SolutionRootItemViewModel(null, displayName);
            _Root.Add(_SolutionRootItem);

            return _SolutionRootItem;
        }

        /// <summary>
        /// Adds another child item below the root item in the collection.
        /// This will throw an Exception if parent is null.
        /// </summary>
        /// <param name="itemName"></param>
        /// <param name="itemType"></param>
        /// <returns></returns>
        public ISolutionBaseItem AddRootChild(
            string itemName,
            SolutionItemType itemType)
        {
            if (_SolutionRootItem == null)
                throw new System.Exception("Solution Root Item must be created BEFORE adding children!");

            return AddChild(itemName, itemType, _SolutionRootItem);
        }

        /// <summary>
        /// Adds another file (child) item below the parent item.
        /// This will throw an Exception if parent is null.
        /// </summary>
        /// <param name="itemName"></param>
        /// <param name="parent"></param>
        /// <param name="itemType"></param>
        /// <returns></returns>
        public ISolutionBaseItem AddChild(
            string itemName,
            SolutionItemType itemType,
            ISolutionBaseItem parent)
        {
            var internalItem = parent as ISolutionItem;

            if (internalItem == null)
                throw new System.ArgumentException("Paremeter parent cannot have children.");

            switch (itemType)
            {
                case SolutionItemType.SolutionRootItem:
                    return AddSolutionRootItem(itemName);

                case SolutionItemType.File:
                    return internalItem.AddFile(itemName);

                case SolutionItemType.Folder:
                    return internalItem.AddFolder(itemName);

                case SolutionItemType.Project:
                    return internalItem.AddProject(itemName);
                default:
                    throw new ArgumentOutOfRangeException(itemType.ToString());
            }
        }
        #endregion methods
    }
}
