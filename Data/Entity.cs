using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CsgoHackPlayground.Data
{
    interface IEntity
    {
        public int health { get; }
    }
    class Entity : IEntity
    {
        public int health { get; private set; }


    }
}
