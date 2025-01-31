﻿using System.Configuration;

using Contour.Configurator.Configuration;

namespace Contour.Configurator
{
    /// <summary>
    /// Конфигурационный элемент для установки параметров динамической маршрутизации.
    /// </summary>
    public class DynamicElement : ConfigurationElement, IDynamic
    {
        /// <summary>
        /// Включение динамической маршрутизации для исходящих сообщений.
        /// </summary>
        [ConfigurationProperty("outgoing", IsRequired = true)]
        public bool? Outgoing
        {
            get
            {
                return (bool?)(base["outgoing"]);
            }
        }
    }
}
