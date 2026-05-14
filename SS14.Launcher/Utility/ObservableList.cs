using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace SS14.Launcher.Utility;

/// <summary>
/// Represents a dynamic data collection that provides notifications when items get added or removed, or when the whole
/// list is refreshed.
///
/// Unlike <see cref="ObservableCollection{T}" />, <see cref="ObservableList{T}" /> also exposes range operations. These
/// only trigger <see cref="ObservableCollection{T}.CollectionChanged" /> events once per range operation.
/// </summary>
/// <typeparam name="T">The type of elements in the collection.</typeparam>
public class ObservableList<T> : ObservableCollection<T>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ObservableList{T}" /> class that contains elements copied from the
    /// specified collection.
    /// </summary>
    /// <param name="collection">The collection from which the elements are copied.</param>
    public ObservableList(IEnumerable<T> collection) : base(collection)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ObservableList{T}" /> class.
    /// </summary>
    public ObservableList() : base()
    {
    }

    /// <summary>
    /// Replace all elements in the <see cref="ObservableList{T}" /> with the elements of the specified collection.
    /// </summary>
    public void SetItems(IEnumerable<T> collection)
    {
        Items.Clear();

        foreach (var item in collection)
        {
            Items.Add(item);
        }

        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    /// <summary>
    /// Adds the elements of the specified collection to the end of the <see cref="ObservableList{T}" />.
    /// </summary>
    public void AddRange(IEnumerable<T> collection)
    {
        foreach (var item in collection)
        {
            Items.Add(item);
        }

        new List<T>().AddRange(new List<T>());

        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
