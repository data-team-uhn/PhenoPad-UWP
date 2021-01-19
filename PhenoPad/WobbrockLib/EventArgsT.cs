using System;
using System.Collections.Generic;
using System.Text;

namespace WobbrockLib
{
    /// <summary>
    /// This class provides a generic event argument template class. It is handy
    /// for avoiding having to subclass EventArgs for simple event argument types.
    /// </summary>
    /// <typeparam name="T">The type of data provided by this event argument.</typeparam>
    public class EventArgs<T> : EventArgs
    {
        private T _data;

        /// <summary>
        /// Constructor for a generic event argument. A single argument is passed
        /// for this EventArgs class.
        /// </summary>
        /// <param name="data"></param>
        public EventArgs(T data)
        {
            _data = data;
        }

        /// <summary>
        /// Gets the data associated with this event argument.
        /// </summary>
        public T Data
        {
            get { return _data; }
        }
    }
}
