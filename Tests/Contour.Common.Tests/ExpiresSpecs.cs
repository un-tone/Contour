﻿using System;

using Contour;

using FluentAssertions;

using NUnit.Framework;
using FluentAssertions.Extensions;

namespace Contour.Common.Tests
{
    /// <summary>
    /// The expires specs.
    /// </summary>
    // ReSharper disable InconsistentNaming
    public class ExpiresSpecs
    {
        /// <summary>
        /// The when_building_expires_from_datetime.
        /// </summary>
        [TestFixture]
        [Category("Unit")]
        public class when_building_expires_from_datetime
        {
            #region Public Methods and Operators

            /// <summary>
            /// The should_create_expires_object_with_date.
            /// </summary>
            [Test]
            public void should_create_expires_object_with_date()
            {
                var dateTime = new DateTime(2014, 5, 6, 7, 8, 9);
                var offset = new TimeSpan(4, 0, 0);
                Expires expires = Expires.At(new DateTimeOffset(dateTime, offset));

                expires.Period.Should().
                    NotHaveValue();
                expires.Date.HasValue.Should().
                    BeTrue();
                expires.Date.Should().
                    Be(new DateTimeOffset(dateTime, offset));
            }

            #endregion
        }

        /// <summary>
        /// The when_building_expires_from_timespan.
        /// </summary>
        [TestFixture]
        [Category("Unit")]
        public class when_building_expires_from_timespan
        {
            #region Public Methods and Operators

            /// <summary>
            /// The should_create_expires_object_with_period.
            /// </summary>
            [Test]
            public void should_create_expires_object_with_period()
            {
                Expires expires = Expires.In(5.Seconds());

                expires.Period.Should().
                    HaveValue();
                expires.Period.Should().
                    Be(5.Seconds());
                expires.Date.HasValue.Should().
                    BeFalse();
            }

            #endregion
        }

        /// <summary>
        /// The when_converting_expires_with_date_to_string.
        /// </summary>
        [TestFixture]
        [Category("Unit")]
        public class when_converting_expires_with_date_to_string
        {
            #region Public Methods and Operators

            /// <summary>
            /// The should_create_string_with_full_date_in_utc.
            /// </summary>
            [Test]
            public void should_create_string_with_full_date_in_utc()
            {
                var dateTime = new DateTime(2014, 5, 6, 7, 8, 9);
                var offset = new TimeSpan(4, 0, 0);
                Expires expires = Expires.At(new DateTimeOffset(dateTime, offset));

                expires.ToString().
                    Should().
                    Be("at 2014-05-06T03:08:09");
            }

            #endregion
        }

        /// <summary>
        /// The when_converting_expires_with_period_to_string.
        /// </summary>
        [TestFixture]
        [Category("Unit")]
        public class when_converting_expires_with_period_to_string
        {
            #region Public Methods and Operators

            /// <summary>
            /// The should_create_string_with_seconds.
            /// </summary>
            [Test]
            public void should_create_string_with_seconds()
            {
                Expires expires = Expires.In(5.Seconds());

                expires.ToString().
                    Should().
                    Be("in 5");
            }

            #endregion
        }

        /// <summary>
        /// The when_parsing_expires_from_date_string.
        /// </summary>
        [TestFixture]
        [Category("Unit")]
        public class when_parsing_expires_from_date_string
        {
            #region Public Methods and Operators

            /// <summary>
            /// The should_create_expires_object_with_date.
            /// </summary>
            [Test]
            public void should_create_expires_object_with_date()
            {
                Expires expires = Expires.Parse("at 2014-05-06T03:08:09");

                expires.Date.Should().
                    Be(new DateTimeOffset(new DateTime(2014, 5, 6, 3, 8, 9), new TimeSpan(0, 0, 0)).ToLocalTime());
            }

            #endregion
        }

        /// <summary>
        /// The when_parsing_expires_from_invalid_string.
        /// </summary>
        [TestFixture]
        [Category("Unit")]
        public class when_parsing_expires_from_invalid_string
        {
            #region Public Methods and Operators

            /// <summary>
            /// The should_throw.
            /// </summary>
            /// <param name="s">
            /// The s.
            /// </param>
            [Test]
            [TestCase("", ExpectedException = typeof(ArgumentException))]
            [TestCase("in", ExpectedException = typeof(ArgumentException))]
            [TestCase("at", ExpectedException = typeof(ArgumentException))]
            [TestCase("in ", ExpectedException = typeof(FormatException))]
            [TestCase("at ", ExpectedException = typeof(FormatException))]
            [TestCase("zzz 5", ExpectedException = typeof(ArgumentException))]
            [TestCase("in 5 6", ExpectedException = typeof(ArgumentException))]
            [TestCase("at 2014-05-06 03:08:09", ExpectedException = typeof(ArgumentException))]
            public void should_throw(string s)
            {
                Expires.Parse(s);
            }

            #endregion
        }

        /// <summary>
        /// The when_parsing_expires_from_period_string.
        /// </summary>
        [TestFixture]
        [Category("Unit")]
        public class when_parsing_expires_from_period_string
        {
            #region Public Methods and Operators

            /// <summary>
            /// The should_create_expires_object_with_period.
            /// </summary>
            [Test]
            public void should_create_expires_object_with_period()
            {
                Expires expires = Expires.Parse("in 15");

                expires.Period.Should().
                    Be(15.Seconds());
            }

            #endregion
        }
    }

    // ReSharper restore InconsistentNaming
}
