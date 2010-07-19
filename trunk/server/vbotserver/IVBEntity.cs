using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VBulletinBot
{
    public interface IVBEntity
    {
        int DatabaseID
        { 
            get; 
            set; 
        }
    }
}
