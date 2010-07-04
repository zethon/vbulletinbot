using System;
using System.Collections;
using System.Text;
using System.Configuration;
using System.Xml;


namespace VBulletinBot
{
    public class BotConfigSection : ConfigurationSection
    {
        public BotConfigSection()
        {
        }

        [ConfigurationProperty("webserviceurl", IsRequired = true)]
        public string WebServiceURL
        {
            get
            {
                return (base["webserviceurl"] as string);
            }

            set
            {
                base["webserviceurl"] = value;
            }
        }

        [ConfigurationProperty("webservicepw", IsRequired = true)]
        public string WebServicePassword
        {
            get
            {
                return (base["webservicepw"] as string);
            }

            set
            {
                base["webservicepw"] = value;
            }
        }

        [ConfigurationProperty("imservices", IsDefaultCollection = false)]
        public IMServiceCollection IMServices
        {
            get
            {
                IMServiceCollection collection = (IMServiceCollection)base["imservices"];
                return collection;
            }
        }

        [ConfigurationProperty("localdatabase", IsRequired = true)]
        public string LocalDatabase
        {
            get
            {
                return (base["localdatabase"] as string);
            }
        }

        [ConfigurationProperty("autointerval", IsRequired = true)]
        public string  AutoInterval
        {
            get
            {
                return (base["autointerval"] as string);
            }
        }
    }

    public class IMServiceElement : ConfigurationElement
    {
        public IMServiceElement()
        {
        }

        [ConfigurationProperty("name", IsRequired = true)]
        public string Name
        {
            get
            {
                return (string)this["name"];
            }

            set
            {
                this["name"] = value;
            }
        }

        [ConfigurationProperty("type", IsRequired = true)]
        public string Type
        {
            get
            {
                return (string)this["type"];
            }

            set
            {
                this["type"] = value;
            }
        }

        [ConfigurationProperty("newline", IsRequired = true)]
        public string NewLine
        {
            get
            {
                return (string)this["newline"];
            }

            set
            {
                this["newline"] = value;
            }
        }

        [ConfigurationProperty("screenname", IsRequired = true)]
        public string ScreenName
        {
            get
            {
                return (string)this["screenname"];
            }

            set
            {
                this["screenname"] = value;
            }
        }

        [ConfigurationProperty("password", IsRequired = true)]
        public string Password
        {
            get
            {
                return (string)this["password"];
            }

            set
            {
                this["password"] = value;
            }
        }
    }

    public class IMServiceCollection : ConfigurationElementCollection
    {
        protected override ConfigurationElement CreateNewElement()
        {
            return new IMServiceElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((IMServiceElement)element).Name;
        }

        public new int Count
        {
            get { return base.Count; }
        }

        public IMServiceElement this[int index]
        {
            get
            {
                return (IMServiceElement)BaseGet(index);
            }
        }

        public IMServiceElement this[string Name]
        {
            get
            {
                return (IMServiceElement)BaseGet(Name);
            }
        }
    }
}
