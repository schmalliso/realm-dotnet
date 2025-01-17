////////////////////////////////////////////////////////////////////////////
//
// Copyright 2016 Realm Inc.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Xml.Serialization;
using Realms.DataBinding;
using Realms.Exceptions;
using Realms.Helpers;
using Realms.Native;
using Realms.Schema;
using Realms.Weaving;

namespace Realms
{
    /// <summary>
    /// Base for any object that can be persisted in a <see cref="Realm"/>.
    /// </summary>
    [Preserve(AllMembers = true, Conditional = false)]
    [Serializable]
    public abstract class RealmObjectBase : INotifyPropertyChanged, IThreadConfined, NotificationsHelper.INotifiable, IReflectableType
    {
        [NonSerialized, XmlIgnore]
        private Lazy<int> _hashCode;

        [NonSerialized, XmlIgnore]
        private Realm _realm;

        [NonSerialized, XmlIgnore]
        private ObjectHandle _objectHandle;

        [NonSerialized, XmlIgnore]
        private Metadata _metadata;

        [NonSerialized, XmlIgnore]
        private NotificationTokenHandle _notificationToken;

        [field: NonSerialized, XmlIgnore]
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:Element should begin with upper-case letter", Justification = "This is the private event - the public is uppercased.")]
        private event PropertyChangedEventHandler _propertyChanged;

        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged
        {
            add
            {
                if (IsManaged && _propertyChanged == null)
                {
                    SubscribeForNotifications();
                }

                _propertyChanged += value;
            }

            remove
            {
                _propertyChanged -= value;

                if (IsManaged &&
                    _propertyChanged == null)
                {
                    UnsubscribeFromNotifications();
                }
            }
        }

        internal ObjectHandle ObjectHandle => _objectHandle;

        internal Metadata ObjectMetadata => _metadata;

        /// <summary>
        /// Gets a value indicating whether the object has been associated with a Realm, either at creation or via
        /// <see cref="Realm.Add{T}(T, bool)"/>.
        /// </summary>
        /// <value><c>true</c> if object belongs to a Realm; <c>false</c> if standalone.</value>
        public bool IsManaged => _realm != null;

        /// <summary>
        /// Gets a value indicating whether this object is managed and represents a row in the database.
        /// If a managed object has been removed from the Realm, it is no longer valid and accessing properties on it
        /// will throw an exception.
        /// Unmanaged objects are always considered valid.
        /// </summary>
        /// <value><c>true</c> if managed and part of the Realm or unmanaged; <c>false</c> if managed but deleted.</value>
        public bool IsValid => _objectHandle?.IsValid != false;

        /// <summary>
        /// Gets a value indicating whether this object is frozen. Frozen objects are immutable
        /// and will not update when writes are made to the Realm. Unlike live objects, frozen
        /// objects can be used across threads.
        /// </summary>
        /// <seealso cref="FrozenObjectsExtensions.Freeze{T}(T)"/>
        public bool IsFrozen => _objectHandle?.IsFrozen == true;

        /// <summary>
        /// Gets the <see cref="Realm"/> instance this object belongs to, or <c>null</c> if it is unmanaged.
        /// </summary>
        /// <value>The <see cref="Realm"/> instance this object belongs to.</value>
        public Realm Realm => _realm;

        /// <summary>
        /// Gets the <see cref="Schema.ObjectSchema"/> instance that describes how the <see cref="Realm"/> this object belongs to sees it.
        /// </summary>
        /// <value>A collection of properties describing the underlying schema of this object.</value>
        public ObjectSchema ObjectSchema => _metadata?.Schema;

        /// <summary>
        /// Gets the number of objects referring to this one via either a to-one or to-many relationship.
        /// </summary>
        /// <remarks>
        /// This property is not observable so the <see cref="PropertyChanged"/> event will not fire when its value changes.
        /// </remarks>
        public int BacklinksCount => _objectHandle?.GetBacklinkCount() ?? 0;

        internal RealmObjectBase FreezeImpl()
        {
            if (!IsManaged)
            {
                throw new RealmException("Unmanaged objects cannot be frozen.");
            }

            var frozenRealm = Realm.Freeze();
            var frozenHandle = _objectHandle.Freeze(frozenRealm.SharedRealmHandle);
            return frozenRealm.MakeObject(ObjectMetadata, frozenHandle);
        }

        /// <inheritdoc/>
        Metadata IMetadataObject.Metadata => ObjectMetadata;

        /// <inheritdoc/>
        IThreadConfinedHandle IThreadConfined.Handle => ObjectHandle;

        internal RealmObjectBase()
        {
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="RealmObjectBase"/> class.
        /// </summary>
        ~RealmObjectBase()
        {
            UnsubscribeFromNotifications();
        }

        internal void SetOwner(Realm realm, ObjectHandle objectHandle, Metadata metadata)
        {
            _realm = realm;
            _objectHandle = objectHandle;
            _metadata = metadata;
            _hashCode = new Lazy<int>(() => _objectHandle.GetObjKey().GetHashCode());

            if (_propertyChanged != null)
            {
                SubscribeForNotifications();
            }
        }

#pragma warning disable SA1600 // Elements should be documented

        protected RealmValue GetValue(string propertyName)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            return _objectHandle.GetValue(propertyName, _metadata, _realm);
        }

        protected void SetValue(string propertyName, RealmValue val)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            var propertyIndex = _metadata.PropertyIndices[propertyName];

            _objectHandle.SetValue(propertyIndex, val, _realm);
        }

        protected void SetValueUnique(string propertyName, RealmValue val)
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            _objectHandle.SetValueUnique(_metadata.PropertyIndices[propertyName], val);
        }

        protected internal IList<T> GetListValue<T>(string propertyName)
        {
            if (!IsManaged)
            {
                return new List<T>();
            }

            _metadata.Schema.TryFindProperty(propertyName, out var property);
            return _objectHandle.GetList<T>(_realm, _metadata.PropertyIndices[propertyName], property.ObjectType);
        }

        protected internal ISet<T> GetSetValue<T>(string propertyName)
        {
            if (!IsManaged)
            {
                return new HashSet<T>(RealmSet<T>.Comparer);
            }

            _metadata.Schema.TryFindProperty(propertyName, out var property);
            return _objectHandle.GetSet<T>(_realm, _metadata.PropertyIndices[propertyName], property.ObjectType);
        }

        protected internal IDictionary<string, TValue> GetDictionaryValue<TValue>(string propertyName)
        {
            if (!IsManaged)
            {
                return new Dictionary<string, TValue>();
            }

            _metadata.Schema.TryFindProperty(propertyName, out var property);
            return _objectHandle.GetDictionary<TValue>(_realm, _metadata.PropertyIndices[propertyName], property.ObjectType);
        }

        protected IQueryable<T> GetBacklinks<T>(string propertyName)
            where T : RealmObjectBase
        {
            Debug.Assert(IsManaged, "Object is not managed, but managed access was attempted");

            var resultsHandle = _objectHandle.GetBacklinks(_metadata.PropertyIndices[propertyName]);
            return GetBacklinksForHandle<T>(propertyName, resultsHandle);
        }

        internal RealmResults<T> GetBacklinksForHandle<T>(string propertyName, ResultsHandle resultsHandle)
            where T : RealmObjectBase
        {
            _metadata.Schema.TryFindProperty(propertyName, out var property);
            var relatedMeta = _realm.Metadata[property.ObjectType];

            return new RealmResults<T>(_realm, resultsHandle, relatedMeta);
        }

#pragma warning restore SA1600 // Elements should be documented

        /// <summary>
        /// Returns all the objects that link to this object in the specified relationship.
        /// </summary>
        /// <param name="objectType">The type of the object that is on the other end of the relationship.</param>
        /// <param name="property">The property that is on the other end of the relationship.</param>
        /// <returns>A queryable collection containing all objects of <c>objectType</c> that link to the current object via <c>property</c>.</returns>
        public IQueryable<dynamic> GetBacklinks(string objectType, string property)
        {
            Argument.Ensure(Realm.Metadata.TryGetValue(objectType, out var relatedMeta), $"Could not find schema for type {objectType}", nameof(objectType));
            Argument.Ensure(relatedMeta.PropertyIndices.ContainsKey(property), $"Type {objectType} does not contain property {property}", nameof(property));

            var resultsHandle = ObjectHandle.GetBacklinksForType(relatedMeta.TableKey, relatedMeta.PropertyIndices[property]);
            if (relatedMeta.Schema.IsEmbedded)
            {
                return new RealmResults<EmbeddedObject>(Realm, resultsHandle, relatedMeta);
            }

            return new RealmResults<RealmObject>(Realm, resultsHandle, relatedMeta);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            // If parameter is null, return false.
            if (obj is null)
            {
                return false;
            }

            // Optimization for a common success case.
            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            // If run-time types are not exactly the same, return false.
            if (!(obj is RealmObjectBase robj))
            {
                return false;
            }

            // standalone objects cannot participate in the same store check
            if (!IsManaged || !robj.IsManaged)
            {
                return false;
            }

            if (ObjectSchema.Name != robj.ObjectSchema.Name)
            {
                return false;
            }

            // Return true if the fields match.
            // Note that the base class is not invoked because it is
            // System.Object, which defines Equals as reference equality.
            return ObjectHandle.Equals(robj.ObjectHandle);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            // _hashCode is only set for managed objects - for unmanaged ones, we
            // fall back to the default behavior.
            return _hashCode?.Value ?? base.GetHashCode();
        }

        /// <summary>
        /// Allows you to raise the PropertyChanged event.
        /// </summary>
        /// <param name="propertyName">The name of the property that has changed. If not specified, we'll use the caller name.</param>
        protected void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            _propertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            OnPropertyChanged(propertyName);
        }

        /// <summary>
        /// Called when a property has changed on this class.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        /// <remarks>
        /// For this method to be called, you need to have first subscribed to <see cref="PropertyChanged"/>.
        /// This can be used to react to changes to the current object, e.g. raising <see cref="PropertyChanged"/> for computed properties.
        /// </remarks>
        /// <example>
        /// <code>
        /// class MyClass : RealmObject
        /// {
        ///     public int StatusCodeRaw { get; set; }
        ///     public StatusCodeEnum StatusCode => (StatusCodeEnum)StatusCodeRaw;
        ///     protected override void OnPropertyChanged(string propertyName)
        ///     {
        ///         if (propertyName == nameof(StatusCodeRaw))
        ///         {
        ///             RaisePropertyChanged(nameof(StatusCode));
        ///         }
        ///     }
        /// }
        /// </code>
        /// Here, we have a computed property that depends on a persisted one. In order to notify any <see cref="PropertyChanged"/>
        /// subscribers that <c>StatusCode</c> has changed, we override <see cref="OnPropertyChanged"/> and
        /// raise <see cref="PropertyChanged"/> manually by calling <see cref="RaisePropertyChanged"/>.
        /// </example>
        protected virtual void OnPropertyChanged(string propertyName)
        {
        }

        /// <summary>
        /// Called when the object has been managed by a Realm.
        /// </summary>
        /// <remarks>
        /// This method will be called either when a managed object is materialized or when an unmanaged object has been
        /// added to the Realm. It can be useful for providing some initialization logic as when the constructor is invoked,
        /// it is not yet clear whether the object is managed or not.
        /// </remarks>
        protected internal virtual void OnManaged()
        {
        }

        private void SubscribeForNotifications()
        {
            Debug.Assert(_notificationToken == null, "_notificationToken must be null before subscribing.");

            if (IsFrozen)
            {
                throw new RealmFrozenException("It is not possible to add a change listener to a frozen RealmObjectBase since it never changes.");
            }

            _realm.ExecuteOutsideTransaction(() =>
            {
                if (ObjectHandle.IsValid)
                {
                    var managedObjectHandle = GCHandle.Alloc(this, GCHandleType.Weak);
                    _notificationToken = ObjectHandle.AddNotificationCallback(GCHandle.ToIntPtr(managedObjectHandle), NotificationsHelper.NotificationCallback);
                }
            });
        }

        private void UnsubscribeFromNotifications()
        {
            _notificationToken?.Dispose();
            _notificationToken = null;
        }

        /// <inheritdoc/>
        void NotificationsHelper.INotifiable.NotifyCallbacks(NotifiableObjectHandleBase.CollectionChangeSet? changes, NativeException? exception)
        {
            var managedException = exception?.Convert();

            if (managedException != null)
            {
                Realm.NotifyError(managedException);
            }
            else if (changes.HasValue)
            {
                foreach (int propertyIndex in changes.Value.Properties.AsEnumerable())
                {
                    // Due to a yet another Mono compiler bug, using LINQ fails here :/
                    var i = 0;
                    foreach (var property in ObjectSchema)
                    {
                        // Backlinks should be ignored. See Realm.CreateRealmObjectMetadata
                        if (property.Type.IsComputed())
                        {
                            continue;
                        }

                        if (i == propertyIndex)
                        {
                            RaisePropertyChanged(property.PropertyInfo?.Name ?? property.Name);
                            break;
                        }

                        ++i;
                    }
                }

                if (changes.Value.Deletions.AsEnumerable().Any())
                {
                    RaisePropertyChanged(nameof(IsValid));

                    if (!IsValid)
                    {
                        // We can proactively unsubscribe because the object has been deleted
                        UnsubscribeFromNotifications();
                    }
                }
            }
        }

        [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1600:Elements should be documented", Justification = "This should not be directly accessed by users.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public TypeInfo GetTypeInfo()
        {
            return TypeInfoHelper.GetInfo(this);
        }

        internal class Metadata
        {
            internal readonly TableKey TableKey;

            internal readonly IRealmObjectHelper Helper;

            internal readonly IReadOnlyDictionary<string, IntPtr> PropertyIndices;

            internal readonly ObjectSchema Schema;

            public Metadata(TableKey tableKey, IRealmObjectHelper helper, IDictionary<string, IntPtr> propertyIndices, ObjectSchema schema)
            {
                TableKey = tableKey;
                Helper = helper;
                PropertyIndices = new ReadOnlyDictionary<string, IntPtr>(propertyIndices);
                Schema = schema;
            }
        }
    }
}
