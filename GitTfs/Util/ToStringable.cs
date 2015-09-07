using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sep.Git.Tfs.Util
{
    public struct ToStringable
    {
        private readonly Func<string> _stringable;

        public ToStringable(Func<string> stringable)
        {
            _stringable = stringable;
        }

        public override string ToString()
        {
            return _stringable();
        }
    }
}
