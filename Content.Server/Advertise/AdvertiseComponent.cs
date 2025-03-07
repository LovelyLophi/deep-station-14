using Content.Server.Advertisements;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.Advertise
{
    [RegisterComponent]
    public sealed partial class AdvertiseComponent : Component
    {
        /// <summary>
        ///     Minimum time in seconds to wait before saying a new ad, in seconds. Has to be larger than or equal to 1.
        /// </summary>
        [DataField("minWait")]
        public int MinimumWait = 8 * 60;

        /// <summary>
        ///     Maximum time in seconds to wait before saying a new ad, in seconds. Has to be larger than or equal
        ///     to <see cref="MinimumWait"/>
        /// </summary>
        [DataField("maxWait")]
        public int MaximumWait = 10 * 60;

        /// <summary>
        ///     The identifier for the advertisements pack prototype.
        /// </summary>
        [DataField("pack", customTypeSerializer:typeof(PrototypeIdSerializer<AdvertisementsPackPrototype>), required: true)]
        public string PackPrototypeId { get; private set; } = string.Empty;

        /// <summary>
        ///     The next time an advertisement will be said.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        public TimeSpan NextAdvertisementTime { get; set; } = TimeSpan.Zero;

        /// <summary>
        ///     Whether the entity will say advertisements or not.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        public bool Enabled { get; set; } = true;
    }
}
