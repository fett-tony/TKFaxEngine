/*
 * TKFaxEngine - managed C# port
 *
 * g1050_h.cs
 * Direct type-level port of g1050.h.
 * Original implementation by Steve Underwood.
 * GNU Lesser General Public License version 2.1.
 */

/*
 * TKFaxEngine - managed C# port
 *
 * g1050.cs
 * Direct managed port of g1050.c and its complete G.1050/TIA-921 tables.
 * Original implementation by Steve Underwood.
 * GNU General Public License version 2.
 */

#nullable enable

using System.Globalization;

namespace TKFaxEngineTest.Network.Test;

public sealed class G1050SegmentConstants {
    public double[] prob_loss_rate_change = new double[2];
    public double[,] prob_impulse = new double[2, 2];
    public double impulse_height;
    public double impulse_decay_coeff;
    public double prob_packet_loss;
    public double prob_packet_collision_loss;
}

public sealed class G1050Constants {
    public G1050SegmentConstants[] segment =
    {
        new(), new(), new(), new()
    };
}

public sealed class G1050SegmentModel {
    public double percentage_occupancy;
    public int mtu;
    public double max_jitter;
}

public sealed class G1050CoreModel {
    public double base_regional_delay;
    public double base_intercontinental_delay;
    public double percentage_packet_loss;
    public double max_jitter;
    public double route_flap_interval;
    public double route_flap_delay;
    public double link_failure_interval;
    public double link_failure_duration;
    public double prob_packet_loss;
    public double prob_oos;
}

public sealed class G1050Model {
    public int[] loo = new int[3];
    public G1050SegmentModel sidea_lan = new();
    public G1050SegmentModel sidea_access_link = new();
    public G1050CoreModel core = new();
    public G1050SegmentModel sideb_access_link = new();
    public G1050SegmentModel sideb_lan = new();
}

public sealed class G1050ChannelSpeeds {
    public int sidea_lan_bit_rate;
    public int sidea_lan_multiple_access;
    public int sidea_access_link_bit_rate_ab;
    public int sidea_access_link_bit_rate_ba;
    public int sidea_access_link_qos_enabled;
    public int sideb_lan_bit_rate;
    public int sideb_lan_multiple_access;
    public int sideb_access_link_bit_rate_ab;
    public int sideb_access_link_bit_rate_ba;
    public int sideb_access_link_qos_enabled;
    public double loo;
}

internal sealed class G1050SegmentState {
    internal int link_type;
    internal bool high_loss;
    internal readonly double[] prob_loss_rate_change = new double[2];
    internal readonly double[] prob_impulse = new double[2];
    internal double impulse_height;
    internal double impulse_decay_coeff;
    internal double serial_delay;
    internal double max_jitter;
    internal double prob_packet_loss;
    internal double prob_packet_collision_loss;
    internal double congestion_delay;
    internal bool qos_enabled;
    internal bool multiple_access;
    internal double last_arrival_time;
    internal readonly double[] delays = new double[3 * G1050.G1050_TICKS_PER_SEC];
    internal uint lost_packets;
    internal uint lost_packets_2;
}

internal sealed class G1050CoreState {
    internal int route_flap_counter;
    internal int route_flap_interval;
    internal double route_flap_delta;
    internal int link_failure_counter;
    internal int link_recovery_counter;
    internal int link_failure_interval_ticks;
    internal int link_failure_duration_ticks;
    internal double base_delay;
    internal double max_jitter;
    internal double prob_packet_loss;
    internal double prob_oos;
    internal double last_arrival_time;
    internal double delay_delta;
    internal readonly double[] delays = new double[3 * G1050.G1050_TICKS_PER_SEC];
    internal uint lost_packets;
    internal uint lost_packets_2;
}

internal sealed class G1050QueueElement {
    internal G1050QueueElement? next;
    internal G1050QueueElement? prev;
    internal int seq_no;
    internal double departure_time;
    internal double arrival_time;
    internal byte[] pkt = Array.Empty<byte>();
    internal int len;
}

public sealed class G1050State {
    internal int packet_rate;
    internal int packet_size;
    internal float base_time;
    internal readonly G1050SegmentState[] segment =
    {
        new(), new(), new(), new()
    };
    internal readonly G1050CoreState core = new();
    internal double[] arrival_times_1 = Array.Empty<double>();
    internal double[] arrival_times_2 = Array.Empty<double>();
    internal G1050QueueElement? first;
    internal G1050QueueElement? last;

    public int PacketRate => packet_rate;
    public int PacketSize => packet_size;
    public float BaseTime => base_time;
}

public static class G1050 {
    public const int G1050_TICKS_PER_SEC = 1000;
    public const int SEARCHBACK_PERIOD = 200;
    public const int G1050_LOW_LOSS = 0;
    public const int G1050_HIGH_LOSS = 1;
    public const int G1050_LAN_LINK = 1;
    public const int G1050_ACCESS_LINK = 2;
    private const double PACKET_LOSS_TIME = -1.0;

    private const ulong Drand48Mask = (1UL << 48) - 1;
    private const ulong Drand48Multiplier = 0x5DEECE66DUL;
    private const ulong Drand48Addend = 0xBUL;
    private static ulong _drand48State = 0x1234ABCD330EUL;

    public static readonly G1050Constants[] g1050_constants =
    {
        new G1050Constants
        {
            segment = new[]
            {
            new G1050SegmentConstants
            {
                prob_loss_rate_change = new[] { 0.004, 0.1 },
                prob_impulse = new[,] { { 0.0, 0.0 }, { 0.5, 0.0 } },
                impulse_height = 1.0,
                impulse_decay_coeff = 0.0,
                prob_packet_loss = 0.001,
                prob_packet_collision_loss = 0.15
            },
            new G1050SegmentConstants
            {
                prob_loss_rate_change = new[] { 0.0002, 0.2 },
                prob_impulse = new[,] { { 0.001, 0.0 }, { 0.3, 0.4 } },
                impulse_height = 40.0,
                impulse_decay_coeff = 0.75,
                prob_packet_loss = 0.0005,
                prob_packet_collision_loss = 0.0
            },
            new G1050SegmentConstants
            {
                prob_loss_rate_change = new[] { 0.0002, 0.2 },
                prob_impulse = new[,] { { 0.001, 0.0 }, { 0.3, 0.4 } },
                impulse_height = 40.0,
                impulse_decay_coeff = 0.75,
                prob_packet_loss = 0.0005,
                prob_packet_collision_loss = 0.0
            },
            new G1050SegmentConstants
            {
                prob_loss_rate_change = new[] { 0.004, 0.1 },
                prob_impulse = new[,] { { 0.0, 0.0 }, { 0.5, 0.0 } },
                impulse_height = 1.0,
                impulse_decay_coeff = 0.0,
                prob_packet_loss = 0.001,
                prob_packet_collision_loss = 0.15
            }
            }
        }
    };

    public static readonly G1050ChannelSpeeds[] g1050_speed_patterns =
    {
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 128000,
            sidea_access_link_bit_rate_ba = 768000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 4000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 128000,
            sideb_access_link_bit_rate_ba = 768000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.36
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 128000,
            sidea_access_link_bit_rate_ba = 768000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 128000,
            sideb_access_link_bit_rate_ba = 768000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.72
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 128000,
            sidea_access_link_bit_rate_ba = 768000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 128000,
            sideb_access_link_bit_rate_ba = 768000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.36
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 128000,
            sidea_access_link_bit_rate_ba = 768000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 128000,
            sideb_access_link_bit_rate_ba = 768000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.36
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 128000,
            sidea_access_link_bit_rate_ba = 768000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 128000,
            sideb_access_link_bit_rate_ba = 768000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.36
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 100000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 128000,
            sidea_access_link_bit_rate_ba = 768000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 128000,
            sideb_access_link_bit_rate_ba = 768000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.09
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 128000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 4000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 768000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.72
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 128000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 768000,
            sideb_access_link_qos_enabled = 0,
            loo = 1.47
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 128000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 768000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.84
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 128000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 768000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.75
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 128000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 768000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.855
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 100000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 128000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 768000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.24
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 128000,
            sidea_access_link_bit_rate_ba = 3000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 4000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 768000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.12
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 128000,
            sidea_access_link_bit_rate_ba = 3000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 768000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.42
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 128000,
            sidea_access_link_bit_rate_ba = 3000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 768000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.84
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 128000,
            sidea_access_link_bit_rate_ba = 3000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 768000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.3
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 128000,
            sidea_access_link_bit_rate_ba = 3000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 768000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.93
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 100000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 128000,
            sidea_access_link_bit_rate_ba = 3000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 768000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.39
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 768000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 4000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 128000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.72
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 768000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 128000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 1.47
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 768000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 128000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.84
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 768000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 128000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.75
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 768000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 128000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.855
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 100000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 768000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 128000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.24
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 4000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 1.44
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 3.0
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 1.92
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 1.563
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 2.0
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 100000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.64
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 3000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 4000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.24
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 3000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.85
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 3000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 1.72
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 3000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.625
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 3000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 2.025
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 100000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 3000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 1.04
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 768000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 4000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 128000,
            sideb_access_link_bit_rate_ba = 3000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.12
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 768000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 128000,
            sideb_access_link_bit_rate_ba = 3000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.42
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 768000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 128000,
            sideb_access_link_bit_rate_ba = 3000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.84
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 768000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 128000,
            sideb_access_link_bit_rate_ba = 3000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.3
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 768000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 128000,
            sideb_access_link_bit_rate_ba = 3000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.93
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 100000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 768000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 128000,
            sideb_access_link_bit_rate_ba = 3000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.39
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 4000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 3000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.24
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 3000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.85
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 3000000,
            sideb_access_link_qos_enabled = 0,
            loo = 1.72
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 3000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.625
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 3000000,
            sideb_access_link_qos_enabled = 0,
            loo = 2.025
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 100000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 3000000,
            sideb_access_link_qos_enabled = 0,
            loo = 1.04
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 3000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 4000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 3000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.04
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 3000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 3000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.2
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 3000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 3000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.52
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 3000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 3000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.25
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 3000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 3000000,
            sideb_access_link_qos_enabled = 0,
            loo = 1.3
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 100000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 3000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 3000000,
            sideb_access_link_qos_enabled = 0,
            loo = 1.69
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 128000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 768000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.09
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 128000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 768000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.36
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 128000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 768000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.09
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 128000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 768000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.405
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 100000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 128000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 768000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.18
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 128000,
            sidea_access_link_bit_rate_ba = 7000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 768000,
            sideb_access_link_bit_rate_ba = 768000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.27
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 128000,
            sidea_access_link_bit_rate_ba = 7000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 768000,
            sideb_access_link_bit_rate_ba = 768000,
            sideb_access_link_qos_enabled = 0,
            loo = 1.08
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 128000,
            sidea_access_link_bit_rate_ba = 7000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 768000,
            sideb_access_link_bit_rate_ba = 768000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.27
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 128000,
            sidea_access_link_bit_rate_ba = 7000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 768000,
            sideb_access_link_bit_rate_ba = 768000,
            sideb_access_link_qos_enabled = 0,
            loo = 1.215
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 100000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 128000,
            sidea_access_link_bit_rate_ba = 7000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 768000,
            sideb_access_link_bit_rate_ba = 768000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.54
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 128000,
            sidea_access_link_bit_rate_ba = 13000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 768000,
            sideb_access_link_bit_rate_ba = 13000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.03
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 128000,
            sidea_access_link_bit_rate_ba = 13000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 768000,
            sideb_access_link_bit_rate_ba = 13000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.12
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 128000,
            sidea_access_link_bit_rate_ba = 13000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 768000,
            sideb_access_link_bit_rate_ba = 13000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.03
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 128000,
            sidea_access_link_bit_rate_ba = 13000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 768000,
            sideb_access_link_bit_rate_ba = 13000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.135
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 100000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 128000,
            sidea_access_link_bit_rate_ba = 13000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 768000,
            sideb_access_link_bit_rate_ba = 13000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.06
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 1536000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.18
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 1536000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.72
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 1536000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.188
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 1536000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.87
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 100000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 1536000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.48
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 7000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 768000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.54
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 7000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 768000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 2.16
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 7000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 768000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.563
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 7000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 768000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 2.61
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 100000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 7000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 768000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 1.44
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 13000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 1536000,
            sideb_access_link_bit_rate_ba = 13000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.06
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 13000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 1536000,
            sideb_access_link_bit_rate_ba = 13000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.24
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 13000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 1536000,
            sideb_access_link_bit_rate_ba = 13000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.063
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 13000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 1536000,
            sideb_access_link_bit_rate_ba = 13000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.29
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 100000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 13000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 1536000,
            sideb_access_link_bit_rate_ba = 13000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.16
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 1536000,
            sideb_access_link_bit_rate_ba = 3000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.03
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 1536000,
            sideb_access_link_bit_rate_ba = 3000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.12
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 1536000,
            sideb_access_link_bit_rate_ba = 3000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.075
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 1536000,
            sideb_access_link_bit_rate_ba = 3000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.495
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 100000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 1536000,
            sideb_access_link_bit_rate_ba = 3000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.78
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 7000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 768000,
            sideb_access_link_bit_rate_ba = 3000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.09
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 7000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 768000,
            sideb_access_link_bit_rate_ba = 3000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.36
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 7000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 768000,
            sideb_access_link_bit_rate_ba = 3000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.225
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 7000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 768000,
            sideb_access_link_bit_rate_ba = 3000000,
            sideb_access_link_qos_enabled = 0,
            loo = 1.485
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 100000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 7000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 768000,
            sideb_access_link_bit_rate_ba = 3000000,
            sideb_access_link_qos_enabled = 0,
            loo = 2.34
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 13000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 3000000,
            sideb_access_link_bit_rate_ba = 13000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.01
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 13000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 3000000,
            sideb_access_link_bit_rate_ba = 13000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.04
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 13000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 3000000,
            sideb_access_link_bit_rate_ba = 13000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.025
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 13000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 3000000,
            sideb_access_link_bit_rate_ba = 13000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.165
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 100000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 384000,
            sidea_access_link_bit_rate_ba = 13000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 3000000,
            sideb_access_link_bit_rate_ba = 13000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.26
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 768000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 128000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.09
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 768000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 128000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.09
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 768000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 128000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.405
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 768000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 128000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.36
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 100000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 768000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 128000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.18
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 1536000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.18
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 1536000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.188
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 1536000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.87
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 1536000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.72
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 100000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 1536000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.48
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 1536000,
            sidea_access_link_bit_rate_ba = 3000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.03
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 1536000,
            sidea_access_link_bit_rate_ba = 3000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.075
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 1536000,
            sidea_access_link_bit_rate_ba = 3000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.495
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 1536000,
            sidea_access_link_bit_rate_ba = 3000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.12
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 100000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 1536000,
            sidea_access_link_bit_rate_ba = 3000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.78
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 768000,
            sidea_access_link_bit_rate_ba = 768000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 128000,
            sideb_access_link_bit_rate_ba = 7000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.27
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 768000,
            sidea_access_link_bit_rate_ba = 768000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 128000,
            sideb_access_link_bit_rate_ba = 7000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.27
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 768000,
            sidea_access_link_bit_rate_ba = 768000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 128000,
            sideb_access_link_bit_rate_ba = 7000000,
            sideb_access_link_qos_enabled = 0,
            loo = 1.215
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 768000,
            sidea_access_link_bit_rate_ba = 768000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 128000,
            sideb_access_link_bit_rate_ba = 7000000,
            sideb_access_link_qos_enabled = 0,
            loo = 1.08
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 100000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 768000,
            sidea_access_link_bit_rate_ba = 768000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 128000,
            sideb_access_link_bit_rate_ba = 7000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.54
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 768000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 7000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.54
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 768000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 7000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.563
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 768000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 7000000,
            sideb_access_link_qos_enabled = 0,
            loo = 2.61
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 768000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 7000000,
            sideb_access_link_qos_enabled = 0,
            loo = 2.16
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 100000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 768000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 7000000,
            sideb_access_link_qos_enabled = 0,
            loo = 1.44
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 768000,
            sidea_access_link_bit_rate_ba = 3000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 7000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.09
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 768000,
            sidea_access_link_bit_rate_ba = 3000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 7000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.225
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 768000,
            sidea_access_link_bit_rate_ba = 3000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 7000000,
            sideb_access_link_qos_enabled = 0,
            loo = 1.485
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 768000,
            sidea_access_link_bit_rate_ba = 3000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 7000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.36
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 100000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 768000,
            sidea_access_link_bit_rate_ba = 3000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 7000000,
            sideb_access_link_qos_enabled = 0,
            loo = 2.34
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 768000,
            sidea_access_link_bit_rate_ba = 13000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 128000,
            sideb_access_link_bit_rate_ba = 13000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.03
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 768000,
            sidea_access_link_bit_rate_ba = 13000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 128000,
            sideb_access_link_bit_rate_ba = 13000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.03
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 768000,
            sidea_access_link_bit_rate_ba = 13000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 128000,
            sideb_access_link_bit_rate_ba = 13000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.135
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 768000,
            sidea_access_link_bit_rate_ba = 13000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 128000,
            sideb_access_link_bit_rate_ba = 13000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.12
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 100000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 768000,
            sidea_access_link_bit_rate_ba = 13000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 128000,
            sideb_access_link_bit_rate_ba = 13000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.06
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 1536000,
            sidea_access_link_bit_rate_ba = 13000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 13000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.06
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 1536000,
            sidea_access_link_bit_rate_ba = 13000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 13000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.063
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 1536000,
            sidea_access_link_bit_rate_ba = 13000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 13000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.29
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 1536000,
            sidea_access_link_bit_rate_ba = 13000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 13000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.24
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 100000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 1536000,
            sidea_access_link_bit_rate_ba = 13000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 13000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.16
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 3000000,
            sidea_access_link_bit_rate_ba = 13000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 13000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.01
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 3000000,
            sidea_access_link_bit_rate_ba = 13000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 13000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.025
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 3000000,
            sidea_access_link_bit_rate_ba = 13000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 13000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.165
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 4000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 3000000,
            sidea_access_link_bit_rate_ba = 13000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 13000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.04
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 100000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 3000000,
            sidea_access_link_bit_rate_ba = 13000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 384000,
            sideb_access_link_bit_rate_ba = 13000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.26
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 1536000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 1536000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.023
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 1536000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 1536000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.18
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 100000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 1536000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 1536000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.36
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 1536000,
            sidea_access_link_bit_rate_ba = 7000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 768000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.068
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 1536000,
            sidea_access_link_bit_rate_ba = 7000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 768000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.54
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 100000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 1536000,
            sidea_access_link_bit_rate_ba = 7000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 768000,
            sideb_access_link_bit_rate_ba = 1536000,
            sideb_access_link_qos_enabled = 0,
            loo = 1.08
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 1536000,
            sidea_access_link_bit_rate_ba = 13000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 1536000,
            sideb_access_link_bit_rate_ba = 13000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.015
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 1536000,
            sidea_access_link_bit_rate_ba = 13000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 1536000,
            sideb_access_link_bit_rate_ba = 13000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.12
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 100000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 1536000,
            sidea_access_link_bit_rate_ba = 13000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 1536000,
            sideb_access_link_bit_rate_ba = 13000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.24
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 768000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 1536000,
            sideb_access_link_bit_rate_ba = 7000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.068
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 768000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 1536000,
            sideb_access_link_bit_rate_ba = 7000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.54
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 100000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 768000,
            sidea_access_link_bit_rate_ba = 1536000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 1536000,
            sideb_access_link_bit_rate_ba = 7000000,
            sideb_access_link_qos_enabled = 0,
            loo = 1.08
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 768000,
            sidea_access_link_bit_rate_ba = 7000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 768000,
            sideb_access_link_bit_rate_ba = 7000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.203
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 768000,
            sidea_access_link_bit_rate_ba = 7000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 768000,
            sideb_access_link_bit_rate_ba = 7000000,
            sideb_access_link_qos_enabled = 0,
            loo = 1.62
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 100000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 768000,
            sidea_access_link_bit_rate_ba = 7000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 768000,
            sideb_access_link_bit_rate_ba = 7000000,
            sideb_access_link_qos_enabled = 0,
            loo = 3.24
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 768000,
            sidea_access_link_bit_rate_ba = 13000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 7000000,
            sideb_access_link_bit_rate_ba = 13000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.023
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 768000,
            sidea_access_link_bit_rate_ba = 13000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 7000000,
            sideb_access_link_bit_rate_ba = 13000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.18
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 100000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 768000,
            sidea_access_link_bit_rate_ba = 13000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 7000000,
            sideb_access_link_bit_rate_ba = 13000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.36
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 7000000,
            sidea_access_link_bit_rate_ba = 13000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 768000,
            sideb_access_link_bit_rate_ba = 13000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.023
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 7000000,
            sidea_access_link_bit_rate_ba = 13000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 768000,
            sideb_access_link_bit_rate_ba = 13000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.18
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 100000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 7000000,
            sidea_access_link_bit_rate_ba = 13000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 768000,
            sideb_access_link_bit_rate_ba = 13000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.36
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 13000000,
            sidea_access_link_bit_rate_ba = 13000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 20000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 13000000,
            sideb_access_link_bit_rate_ba = 13000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.003
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 20000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 13000000,
            sidea_access_link_bit_rate_ba = 13000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 13000000,
            sideb_access_link_bit_rate_ba = 13000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.02
        },
        new G1050ChannelSpeeds
        {
            sidea_lan_bit_rate = 100000000,
            sidea_lan_multiple_access = 0,
            sidea_access_link_bit_rate_ab = 13000000,
            sidea_access_link_bit_rate_ba = 13000000,
            sidea_access_link_qos_enabled = 0,
            sideb_lan_bit_rate = 100000000,
            sideb_lan_multiple_access = 0,
            sideb_access_link_bit_rate_ab = 13000000,
            sideb_access_link_bit_rate_ba = 13000000,
            sideb_access_link_qos_enabled = 0,
            loo = 0.04
        }
    };

    public static readonly G1050Model[] g1050_standard_models =
    {
        new G1050Model
        {
            loo = new[] { 0, 0, 0 },
            sidea_lan = new G1050SegmentModel { percentage_occupancy = 0.0, mtu = 1508, max_jitter = 0.0 },
            sidea_access_link = new G1050SegmentModel { percentage_occupancy = 0.0, mtu = 512, max_jitter = 0.0 },
            core = new G1050CoreModel { base_regional_delay = 0.0, base_intercontinental_delay = 0.0, percentage_packet_loss = 0.0, max_jitter = 0.0, route_flap_interval = 0.0, route_flap_delay = 0.0, link_failure_interval = 0.0, link_failure_duration = 0.0, prob_packet_loss = 0.0, prob_oos = 0.0 },
            sideb_access_link = new G1050SegmentModel { percentage_occupancy = 0.0, mtu = 512, max_jitter = 0.0 },
            sideb_lan = new G1050SegmentModel { percentage_occupancy = 0.0, mtu = 1508, max_jitter = 0.0 }
        },
        new G1050Model
        {
            loo = new[] { 50, 5, 5 },
            sidea_lan = new G1050SegmentModel { percentage_occupancy = 1.0, mtu = 1508, max_jitter = 0.0015 },
            sidea_access_link = new G1050SegmentModel { percentage_occupancy = 0.0, mtu = 512, max_jitter = 0.0 },
            core = new G1050CoreModel { base_regional_delay = 0.004, base_intercontinental_delay = 0.016, percentage_packet_loss = 0.0, max_jitter = 0.005, route_flap_interval = 0.0, route_flap_delay = 0.0, link_failure_interval = 0.0, link_failure_duration = 0.0, prob_packet_loss = 0.0, prob_oos = 0.0 },
            sideb_access_link = new G1050SegmentModel { percentage_occupancy = 0.0, mtu = 512, max_jitter = 0.0 },
            sideb_lan = new G1050SegmentModel { percentage_occupancy = 1.0, mtu = 1508, max_jitter = 0.0015 }
        },
        new G1050Model
        {
            loo = new[] { 30, 25, 5 },
            sidea_lan = new G1050SegmentModel { percentage_occupancy = 2.0, mtu = 1508, max_jitter = 0.0015 },
            sidea_access_link = new G1050SegmentModel { percentage_occupancy = 1.0, mtu = 512, max_jitter = 0.0 },
            core = new G1050CoreModel { base_regional_delay = 0.008, base_intercontinental_delay = 0.032, percentage_packet_loss = 0.01, max_jitter = 0.01, route_flap_interval = 3600.0, route_flap_delay = 0.002, link_failure_interval = 3600.0, link_failure_duration = 0.064, prob_packet_loss = 0.0, prob_oos = 0.0 },
            sideb_access_link = new G1050SegmentModel { percentage_occupancy = 1.0, mtu = 512, max_jitter = 0.0 },
            sideb_lan = new G1050SegmentModel { percentage_occupancy = 2.0, mtu = 1508, max_jitter = 0.0015 }
        },
        new G1050Model
        {
            loo = new[] { 15, 30, 10 },
            sidea_lan = new G1050SegmentModel { percentage_occupancy = 3.0, mtu = 1508, max_jitter = 0.0015 },
            sidea_access_link = new G1050SegmentModel { percentage_occupancy = 2.0, mtu = 1508, max_jitter = 0.0 },
            core = new G1050CoreModel { base_regional_delay = 0.016, base_intercontinental_delay = 0.064, percentage_packet_loss = 0.02, max_jitter = 0.016, route_flap_interval = 1800.0, route_flap_delay = 0.004, link_failure_interval = 1800.0, link_failure_duration = 0.128, prob_packet_loss = 0.0, prob_oos = 0.0 },
            sideb_access_link = new G1050SegmentModel { percentage_occupancy = 2.0, mtu = 1508, max_jitter = 0.0 },
            sideb_lan = new G1050SegmentModel { percentage_occupancy = 3.0, mtu = 1508, max_jitter = 0.0015 }
        },
        new G1050Model
        {
            loo = new[] { 5, 25, 15 },
            sidea_lan = new G1050SegmentModel { percentage_occupancy = 5.0, mtu = 1508, max_jitter = 0.0015 },
            sidea_access_link = new G1050SegmentModel { percentage_occupancy = 4.0, mtu = 1508, max_jitter = 0.0 },
            core = new G1050CoreModel { base_regional_delay = 0.032, base_intercontinental_delay = 0.128, percentage_packet_loss = 0.04, max_jitter = 0.04, route_flap_interval = 900.0, route_flap_delay = 0.008, link_failure_interval = 900.0, link_failure_duration = 0.256, prob_packet_loss = 0.0, prob_oos = 0.0 },
            sideb_access_link = new G1050SegmentModel { percentage_occupancy = 4.0, mtu = 1508, max_jitter = 0.0 },
            sideb_lan = new G1050SegmentModel { percentage_occupancy = 5.0, mtu = 1508, max_jitter = 0.0015 }
        },
        new G1050Model
        {
            loo = new[] { 0, 10, 20 },
            sidea_lan = new G1050SegmentModel { percentage_occupancy = 8.0, mtu = 1508, max_jitter = 0.0015 },
            sidea_access_link = new G1050SegmentModel { percentage_occupancy = 8.0, mtu = 1508, max_jitter = 0.0 },
            core = new G1050CoreModel { base_regional_delay = 0.064, base_intercontinental_delay = 0.196, percentage_packet_loss = 0.1, max_jitter = 0.07, route_flap_interval = 480.0, route_flap_delay = 0.016, link_failure_interval = 480.0, link_failure_duration = 0.4, prob_packet_loss = 0.0, prob_oos = 0.0 },
            sideb_access_link = new G1050SegmentModel { percentage_occupancy = 8.0, mtu = 1508, max_jitter = 0.0 },
            sideb_lan = new G1050SegmentModel { percentage_occupancy = 8.0, mtu = 1508, max_jitter = 0.0015 }
        },
        new G1050Model
        {
            loo = new[] { 0, 0, 25 },
            sidea_lan = new G1050SegmentModel { percentage_occupancy = 12.0, mtu = 1508, max_jitter = 0.0015 },
            sidea_access_link = new G1050SegmentModel { percentage_occupancy = 15.0, mtu = 1508, max_jitter = 0.0 },
            core = new G1050CoreModel { base_regional_delay = 0.128, base_intercontinental_delay = 0.256, percentage_packet_loss = 0.2, max_jitter = 0.1, route_flap_interval = 240.0, route_flap_delay = 0.032, link_failure_interval = 240.0, link_failure_duration = 0.8, prob_packet_loss = 0.0, prob_oos = 0.0 },
            sideb_access_link = new G1050SegmentModel { percentage_occupancy = 15.0, mtu = 1508, max_jitter = 0.0 },
            sideb_lan = new G1050SegmentModel { percentage_occupancy = 12.0, mtu = 1508, max_jitter = 0.0015 }
        },
        new G1050Model
        {
            loo = new[] { 0, 0, 15 },
            sidea_lan = new G1050SegmentModel { percentage_occupancy = 16.0, mtu = 1508, max_jitter = 0.0015 },
            sidea_access_link = new G1050SegmentModel { percentage_occupancy = 30.0, mtu = 1508, max_jitter = 0.0 },
            core = new G1050CoreModel { base_regional_delay = 0.256, base_intercontinental_delay = 0.512, percentage_packet_loss = 0.5, max_jitter = 0.15, route_flap_interval = 120.0, route_flap_delay = 0.064, link_failure_interval = 120.0, link_failure_duration = 1.6, prob_packet_loss = 0.0, prob_oos = 0.0 },
            sideb_access_link = new G1050SegmentModel { percentage_occupancy = 30.0, mtu = 1508, max_jitter = 0.0 },
            sideb_lan = new G1050SegmentModel { percentage_occupancy = 16.0, mtu = 1508, max_jitter = 0.0015 }
        },
        new G1050Model
        {
            loo = new[] { 0, 0, 5 },
            sidea_lan = new G1050SegmentModel { percentage_occupancy = 20.0, mtu = 1508, max_jitter = 0.0015 },
            sidea_access_link = new G1050SegmentModel { percentage_occupancy = 50.0, mtu = 1508, max_jitter = 0.0 },
            core = new G1050CoreModel { base_regional_delay = 0.512, base_intercontinental_delay = 0.768, percentage_packet_loss = 1.0, max_jitter = 0.5, route_flap_interval = 60.0, route_flap_delay = 0.128, link_failure_interval = 60.0, link_failure_duration = 3.0, prob_packet_loss = 1.0, prob_oos = 1.0 },
            sideb_access_link = new G1050SegmentModel { percentage_occupancy = 50.0, mtu = 1508, max_jitter = 0.0 },
            sideb_lan = new G1050SegmentModel { percentage_occupancy = 20.0, mtu = 1508, max_jitter = 0.0015 }
        }
    };

    public static void srand48(long seed) {
        _drand48State = ((((ulong)seed) << 16) | 0x330EUL) & Drand48Mask;
    }

    public static double drand48() {
        _drand48State = (Drand48Multiplier * _drand48State + Drand48Addend) & Drand48Mask;
        return _drand48State / 281474976710656.0;
    }

    private static void q1050_rand_init() {
        srand48(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }

    private static double q1050_rand() => drand48();

    private static double scale_probability(double prob, double scale) {
        return 1.0 - Math.Pow(1.0 - prob, scale);
    }

    private static void g1050_segment_init(
        G1050SegmentState s,
        int linkType,
        G1050SegmentConstants constants,
        G1050SegmentModel parms,
        int bitRate,
        int multipleAccess,
        int qosEnabled,
        int packetSize,
        int packetRate) {
        double packetInterval = 1000.0 / packetRate;
        s.link_type = linkType;
        s.prob_loss_rate_change[0] = scale_probability(
            constants.prob_loss_rate_change[0] * parms.percentage_occupancy,
            1.0 / packetInterval);
        s.serial_delay = packetSize * 8.0 / bitRate;

        if (linkType == G1050_LAN_LINK) {
            s.prob_loss_rate_change[1] = scale_probability(
                constants.prob_loss_rate_change[1], 1.0 / packetInterval);
            s.prob_impulse[0] = constants.prob_impulse[0, 0];
            s.prob_impulse[1] = constants.prob_impulse[1, 0];
            s.impulse_decay_coeff = constants.impulse_decay_coeff;
            s.impulse_height = parms.mtu * (8.0 / bitRate) *
                (1.0 + parms.percentage_occupancy / constants.impulse_height);
        } else if (linkType == G1050_ACCESS_LINK) {
            s.prob_loss_rate_change[1] = scale_probability(
                constants.prob_loss_rate_change[1] / (1.0 + parms.percentage_occupancy),
                1.0 / packetInterval);
            s.prob_impulse[0] = scale_probability(
                constants.prob_impulse[0, 0] + parms.percentage_occupancy / 2000.0,
                1.0 / packetInterval);
            s.prob_impulse[1] = scale_probability(
                constants.prob_impulse[1, 0] +
                constants.prob_impulse[1, 1] * parms.percentage_occupancy / 100.0,
                1.0 / packetInterval);
            s.impulse_decay_coeff = 1.0 - scale_probability(
                1.0 - constants.impulse_decay_coeff,
                1.0 / packetInterval);
            double x = (1.0 - constants.impulse_decay_coeff) /
                (1.0 - s.impulse_decay_coeff);
            s.impulse_height = x * parms.mtu * (8.0 / bitRate) *
                (1.0 + parms.percentage_occupancy / constants.impulse_height);
        }

        s.prob_packet_loss = constants.prob_packet_loss * parms.percentage_occupancy;
        s.qos_enabled = qosEnabled != 0;
        s.multiple_access = multipleAccess != 0;
        s.prob_packet_collision_loss = constants.prob_packet_collision_loss;
        s.max_jitter = parms.max_jitter;
        s.high_loss = false;
        s.congestion_delay = 0.0;
        s.last_arrival_time = 0.0;
        s.lost_packets = 0;
        s.lost_packets_2 = 0;
        Array.Clear(s.delays);
    }

    private static void g1050_core_init(G1050CoreState s, G1050CoreModel parms, int packetRate) {
        _ = packetRate;
        s.route_flap_interval = (int)(parms.route_flap_interval * G1050_TICKS_PER_SEC);
        s.route_flap_delta = parms.route_flap_delay;
        s.route_flap_counter = s.route_flap_interval - 99 -
            (int)Math.Floor(s.route_flap_interval * q1050_rand());
        s.link_failure_interval_ticks =
            (int)(parms.link_failure_interval * G1050_TICKS_PER_SEC);
        s.link_failure_duration_ticks =
            (int)Math.Floor(G1050_TICKS_PER_SEC * parms.link_failure_duration);
        s.link_failure_counter = s.link_failure_interval_ticks - 99 -
            (int)Math.Floor(s.link_failure_interval_ticks * q1050_rand());
        s.link_recovery_counter = s.link_failure_duration_ticks;
        s.base_delay = parms.base_regional_delay;
        s.max_jitter = parms.max_jitter;
        s.prob_packet_loss = parms.prob_packet_loss / 100.0;
        s.prob_oos = parms.prob_oos / 100.0;
        s.last_arrival_time = 0.0;
        s.delay_delta = 0.0;
        s.lost_packets = 0;
        s.lost_packets_2 = 0;
        Array.Clear(s.delays);
    }

    private static void g1050_segment_model(
        G1050SegmentState s,
        double[] delays,
        int offset,
        int len) {
        for (int i = 0; i < len; i++) {
            bool lose = false;
            double sliceDelay = s.serial_delay + s.max_jitter * q1050_rand();

            if (!s.qos_enabled) {
                int wasHighLoss = s.high_loss ? 1 : 0;
                if (q1050_rand() < s.prob_loss_rate_change[wasHighLoss])
                    s.high_loss = !s.high_loss;

                double impulse = 0.0;
                if (q1050_rand() < s.prob_impulse[wasHighLoss]) {
                    impulse = s.impulse_height;
                    if (wasHighLoss == 0 || s.link_type == G1050_LAN_LINK)
                        impulse *= q1050_rand();
                }

                if (wasHighLoss != 0 && q1050_rand() < s.prob_packet_loss)
                    lose = true;

                s.congestion_delay =
                    s.congestion_delay * s.impulse_decay_coeff +
                    impulse * (1.0 - s.impulse_decay_coeff);
                sliceDelay += s.congestion_delay;
            }

            if (s.multiple_access && q1050_rand() < s.prob_packet_collision_loss)
                lose = true;

            if (lose) {
                delays[offset + i] = PACKET_LOSS_TIME;
                s.lost_packets++;
            } else {
                delays[offset + i] = sliceDelay;
            }
        }
    }

    private static void g1050_core_model(
        G1050CoreState s,
        double[] delays,
        int offset,
        int len) {
        for (int i = 0; i < len; i++) {
            bool lose = false;
            double jitterDelay = s.base_delay + s.max_jitter * q1050_rand();

            if (--s.route_flap_counter <= 0) {
                s.delay_delta = s.route_flap_delta - s.delay_delta;
                s.route_flap_counter = s.route_flap_interval;
            }

            if (q1050_rand() < s.prob_packet_loss)
                lose = true;

            if (--s.link_failure_counter <= 0) {
                lose = true;
                if (--s.link_recovery_counter <= 0) {
                    s.link_failure_counter = s.link_failure_interval_ticks;
                    s.link_recovery_counter = s.link_failure_duration_ticks;
                    lose = false;
                }
            }

            if (lose) {
                delays[offset + i] = PACKET_LOSS_TIME;
                s.lost_packets++;
            } else {
                delays[offset + i] = jitterDelay + s.delay_delta;
            }
        }
    }

    private static int g1050_segment_delay(
        G1050SegmentState s,
        double baseTime,
        double[] arrivalTimes,
        double[] delays,
        int numPackets) {
        int lostPackets = 0;
        for (int i = 0; i < numPackets; i++) {
            int departureTime = (int)((arrivalTimes[i] + 0.0005 - baseTime) * G1050_TICKS_PER_SEC);
            if (arrivalTimes[i] == PACKET_LOSS_TIME) {
            } else if (delays[departureTime] == PACKET_LOSS_TIME) {
                arrivalTimes[i] = PACKET_LOSS_TIME;
                lostPackets++;
            } else {
                arrivalTimes[i] += delays[departureTime];
                if (arrivalTimes[i] < s.last_arrival_time)
                    arrivalTimes[i] = s.last_arrival_time;
                else
                    s.last_arrival_time = arrivalTimes[i];
            }
        }
        return lostPackets;
    }

    private static int g1050_segment_delay_preserve_order(
        G1050SegmentState s,
        double baseTime,
        double[] arrivalTimesA,
        double[] arrivalTimesB,
        double[] delays,
        int numPackets) {
        _ = s;
        double lastArrivalTime = 0.0;
        double lastArrivalTimeTemp = 0.0;
        int lostPackets = 0;

        for (int i = 0; i < numPackets; i++) {
            int departureTime = (int)((arrivalTimesA[i] + 0.0005 - baseTime) * G1050_TICKS_PER_SEC);
            if (arrivalTimesA[i] == PACKET_LOSS_TIME) {
                arrivalTimesB[i] = PACKET_LOSS_TIME;
            } else if (delays[departureTime] == PACKET_LOSS_TIME) {
                arrivalTimesB[i] = PACKET_LOSS_TIME;
                lostPackets++;
            } else {
                arrivalTimesB[i] = arrivalTimesA[i] + delays[departureTime];
                if (arrivalTimesA[i] < lastArrivalTime) {
                    for (int j = i - 1; j >= 0; j--) {
                        if (arrivalTimesA[j] != PACKET_LOSS_TIME &&
                            arrivalTimesB[j] != PACKET_LOSS_TIME) {
                            if (arrivalTimesA[i] - arrivalTimesA[j] > SEARCHBACK_PERIOD)
                                break;
                            if (arrivalTimesA[j] > arrivalTimesA[i] &&
                                arrivalTimesB[j] < arrivalTimesB[i]) {
                                arrivalTimesB[j] = arrivalTimesB[i];
                            }
                        }
                    }
                } else {
                    lastArrivalTime = arrivalTimesA[i];
                    if (arrivalTimesB[i] < lastArrivalTimeTemp)
                        arrivalTimesB[i] = lastArrivalTimeTemp;
                    else
                        lastArrivalTimeTemp = arrivalTimesB[i];
                }
            }
        }
        return lostPackets;
    }

    private static int g1050_core_delay(
        G1050CoreState s,
        double baseTime,
        double[] arrivalTimes,
        double[] delays,
        int numPackets) {
        int lostPackets = 0;
        for (int i = 0; i < numPackets; i++) {
            int departureTime = (int)((arrivalTimes[i] + 0.0005 - baseTime) * G1050_TICKS_PER_SEC);
            if (arrivalTimes[i] == PACKET_LOSS_TIME) {
            } else if (delays[departureTime] == PACKET_LOSS_TIME) {
                arrivalTimes[i] = PACKET_LOSS_TIME;
                lostPackets++;
            } else {
                arrivalTimes[i] += delays[departureTime];
                if (arrivalTimes[i] < s.last_arrival_time) {
                    if (q1050_rand() >= s.prob_oos)
                        arrivalTimes[i] = s.last_arrival_time;
                } else {
                    s.last_arrival_time = arrivalTimes[i];
                }
            }
        }
        return lostPackets;
    }

    private static void ShiftLeftOneSecond(double[] values) {
        Array.Copy(values, G1050_TICKS_PER_SEC, values, 0, 2 * G1050_TICKS_PER_SEC);
    }

    private static void ShiftLeftPacketRate(double[] values, int packetRate) {
        Array.Copy(values, packetRate, values, 0, 2 * packetRate);
    }

    private static void g1050_simulate_chunk(G1050State s) {
        s.base_time += 1.0f;

        ShiftLeftOneSecond(s.segment[0].delays);
        g1050_segment_model(s.segment[0], s.segment[0].delays, 2 * G1050_TICKS_PER_SEC, G1050_TICKS_PER_SEC);
        ShiftLeftOneSecond(s.segment[1].delays);
        g1050_segment_model(s.segment[1], s.segment[1].delays, 2 * G1050_TICKS_PER_SEC, G1050_TICKS_PER_SEC);
        ShiftLeftOneSecond(s.core.delays);
        g1050_core_model(s.core, s.core.delays, 2 * G1050_TICKS_PER_SEC, G1050_TICKS_PER_SEC);
        ShiftLeftOneSecond(s.segment[2].delays);
        g1050_segment_model(s.segment[2], s.segment[2].delays, 2 * G1050_TICKS_PER_SEC, G1050_TICKS_PER_SEC);
        ShiftLeftOneSecond(s.segment[3].delays);
        g1050_segment_model(s.segment[3], s.segment[3].delays, 2 * G1050_TICKS_PER_SEC, G1050_TICKS_PER_SEC);

        ShiftLeftPacketRate(s.arrival_times_1, s.packet_rate);
        ShiftLeftPacketRate(s.arrival_times_2, s.packet_rate);
        for (int i = 0; i < s.packet_rate; i++) {
            s.arrival_times_1[2 * s.packet_rate + i] =
                s.base_time + 2.0 + (double)i / s.packet_rate;
            s.arrival_times_2[2 * s.packet_rate + i] = 0.0;
        }

        s.segment[0].lost_packets_2 += (uint)g1050_segment_delay(
            s.segment[0], s.base_time, s.arrival_times_1, s.segment[0].delays, s.packet_rate);
        s.segment[1].lost_packets_2 += (uint)g1050_segment_delay(
            s.segment[1], s.base_time, s.arrival_times_1, s.segment[1].delays, s.packet_rate);
        s.core.lost_packets_2 += (uint)g1050_core_delay(
            s.core, s.base_time, s.arrival_times_1, s.core.delays, s.packet_rate);
        s.segment[2].lost_packets_2 += (uint)g1050_segment_delay_preserve_order(
            s.segment[2], s.base_time, s.arrival_times_1, s.arrival_times_2, s.segment[2].delays, s.packet_rate);
        s.segment[3].lost_packets_2 += (uint)g1050_segment_delay_preserve_order(
            s.segment[3], s.base_time, s.arrival_times_2, s.arrival_times_1, s.segment[3].delays, s.packet_rate);
    }

    public static G1050State g1050_init(int model, int speedPattern, int packetSize, int packetRate) {
        if ((uint)model >= (uint)g1050_standard_models.Length)
            throw new ArgumentOutOfRangeException(nameof(model));
        if (speedPattern < 1 || speedPattern > g1050_speed_patterns.Length)
            throw new ArgumentOutOfRangeException(nameof(speedPattern));
        if (packetSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(packetSize));
        if (packetRate <= 0 || packetRate > G1050_TICKS_PER_SEC)
            throw new ArgumentOutOfRangeException(nameof(packetRate));

        int i;
        for (i = 0; i < 10; i++) {
            if (q1050_rand() != 0.0)
                break;
        }
        if (i >= 10)
            q1050_rand_init();

        G1050Constants constants = g1050_constants[0];
        G1050ChannelSpeeds sp = g1050_speed_patterns[speedPattern - 1];
        G1050Model mo = g1050_standard_models[model];
        G1050State s = new() {
            packet_rate = packetRate,
            packet_size = packetSize,
            arrival_times_1 = new double[Math.Max(3 * G1050_TICKS_PER_SEC, 3 * packetRate)],
            arrival_times_2 = new double[Math.Max(3 * G1050_TICKS_PER_SEC, 3 * packetRate)]
        };

        g1050_segment_init(s.segment[0], G1050_LAN_LINK, constants.segment[0], mo.sidea_lan,
            sp.sidea_lan_bit_rate, sp.sidea_lan_multiple_access, 0, packetSize, packetRate);
        g1050_segment_init(s.segment[1], G1050_ACCESS_LINK, constants.segment[1], mo.sidea_access_link,
            sp.sidea_access_link_bit_rate_ab, 0, sp.sidea_access_link_qos_enabled, packetSize, packetRate);
        g1050_core_init(s.core, mo.core, packetRate);
        g1050_segment_init(s.segment[2], G1050_ACCESS_LINK, constants.segment[2], mo.sideb_access_link,
            sp.sideb_access_link_bit_rate_ba, 0, sp.sideb_access_link_qos_enabled, packetSize, packetRate);
        g1050_segment_init(s.segment[3], G1050_LAN_LINK, constants.segment[3], mo.sideb_lan,
            sp.sideb_lan_bit_rate, sp.sideb_lan_multiple_access, 0, packetSize, packetRate);

        s.base_time = 0.0f;
        g1050_segment_model(s.segment[0], s.segment[0].delays, 0, 3 * G1050_TICKS_PER_SEC);
        g1050_segment_model(s.segment[1], s.segment[1].delays, 0, 3 * G1050_TICKS_PER_SEC);
        g1050_core_model(s.core, s.core.delays, 0, 3 * G1050_TICKS_PER_SEC);
        g1050_segment_model(s.segment[2], s.segment[2].delays, 0, 3 * G1050_TICKS_PER_SEC);
        g1050_segment_model(s.segment[3], s.segment[3].delays, 0, 3 * G1050_TICKS_PER_SEC);

        for (i = 0; i < 3 * s.packet_rate; i++) {
            s.arrival_times_1[i] = s.base_time + (double)i / s.packet_rate;
            s.arrival_times_2[i] = 0.0;
        }

        s.segment[0].lost_packets_2 += (uint)g1050_segment_delay(
            s.segment[0], s.base_time, s.arrival_times_1, s.segment[0].delays, s.packet_rate);
        s.segment[1].lost_packets_2 += (uint)g1050_segment_delay(
            s.segment[1], s.base_time, s.arrival_times_1, s.segment[1].delays, s.packet_rate);
        s.core.lost_packets_2 += (uint)g1050_core_delay(
            s.core, s.base_time, s.arrival_times_1, s.core.delays, s.packet_rate);
        s.segment[2].lost_packets_2 += (uint)g1050_segment_delay_preserve_order(
            s.segment[2], s.base_time, s.arrival_times_1, s.arrival_times_2, s.segment[2].delays, s.packet_rate);
        s.segment[3].lost_packets_2 += (uint)g1050_segment_delay_preserve_order(
            s.segment[3], s.base_time, s.arrival_times_2, s.arrival_times_1, s.segment[3].delays, s.packet_rate);

        return s;
    }

    public static int g1050_free(G1050State s) {
        ArgumentNullException.ThrowIfNull(s);
        s.first = null;
        s.last = null;
        return 0;
    }

    public static void g1050_dump_parms(int model, int speedPattern, TextWriter? writer = null) {
        writer ??= Console.Out;
        G1050ChannelSpeeds sp = g1050_speed_patterns[speedPattern - 1];
        G1050Model mo = g1050_standard_models[model];
        writer.WriteLine($"Model {speedPattern}{(char)('A' + model - 1)}");
        writer.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"LOO {mo.loo[0] * sp.loo / 100.0:F6}% {mo.loo[1] * sp.loo / 100.0:F6}% {mo.loo[2] * sp.loo / 100.0:F6}%"));
        writer.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Side A LAN {sp.sidea_lan_bit_rate}bps, {mo.sidea_lan.percentage_occupancy:F3}% occupancy, MTU {mo.sidea_lan.mtu}, {(sp.sidea_lan_multiple_access != 0 ? "" : "no")} MA"));
        writer.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Side A access {sp.sidea_access_link_bit_rate_ab}bps, {mo.sidea_access_link.percentage_occupancy:F3}% occupancy, MTU {mo.sidea_access_link.mtu}, {(sp.sidea_access_link_qos_enabled != 0 ? "" : "no")} QoS"));
        writer.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Core delay {mo.core.base_regional_delay:F4}s ({mo.core.base_intercontinental_delay:F4}s), peak jitter {mo.core.max_jitter:F4}s, prob loss {mo.core.prob_packet_loss:F4}%, prob OOS {mo.core.prob_oos:F4}%"));
        writer.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"     Route flap interval {mo.core.route_flap_interval:F4}s, delay change {mo.core.route_flap_delay:F4}s"));
        writer.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"     Link failure interval {mo.core.link_failure_interval:F4}s, duration {mo.core.link_failure_duration:F4}s"));
        writer.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Side B access {sp.sideb_access_link_bit_rate_ba}bps, {mo.sideb_access_link.percentage_occupancy:F3}% occupancy, MTU {mo.sideb_access_link.mtu}, {(sp.sideb_access_link_qos_enabled != 0 ? "" : "no")} QoS"));
        writer.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Side B LAN {sp.sideb_lan_bit_rate}bps, {mo.sideb_lan.percentage_occupancy:F3}% occupancy, MTU {mo.sideb_lan.mtu}, {(sp.sideb_lan_multiple_access != 0 ? "" : "no")} MA"));
    }

    public static int g1050_put(
        G1050State s,
        byte[] buf,
        int len,
        int seqNo,
        double departureTime) {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(buf);
        if ((uint)len > (uint)buf.Length)
            throw new ArgumentOutOfRangeException(nameof(len));

        while (departureTime >= s.base_time + 1.0)
            g1050_simulate_chunk(s);

        int arrivalIndex = (int)((departureTime - s.base_time) * s.packet_rate + 0.5);
        double arrivalTime = s.arrival_times_1[arrivalIndex];
        if (arrivalTime < 0.0)
            return 0;

        G1050QueueElement element = new() {
            seq_no = seqNo,
            departure_time = departureTime,
            arrival_time = arrivalTime,
            len = len,
            pkt = new byte[len]
        };
        Array.Copy(buf, element.pkt, len);

        if (s.last is null) {
            s.first = element;
            s.last = element;
        } else {
            G1050QueueElement? e;
            for (e = s.last; e is not null; e = e.prev) {
                if (e.arrival_time <= arrivalTime)
                    break;
            }

            if (e is not null) {
                element.next = e.next;
                element.prev = e;
                e.next = element;
            } else {
                element.next = s.first;
                s.first = element;
            }

            if (element.next is not null)
                element.next.prev = element;
            else
                s.last = element;
        }

        return len;
    }

    public static int g1050_get(
        G1050State s,
        byte[] buf,
        int maxLen,
        double currentTime,
        out int seqNo,
        out double departureTime,
        out double arrivalTime) {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(buf);

        G1050QueueElement? element = s.first;
        if (element is null) {
            seqNo = -1;
            departureTime = -1.0;
            arrivalTime = -1.0;
            return -1;
        }

        if (element.arrival_time > currentTime) {
            seqNo = element.seq_no;
            departureTime = element.departure_time;
            arrivalTime = element.arrival_time;
            return -1;
        }

        int len = element.len;
        _ = maxLen;
        if (len > buf.Length)
            throw new ArgumentException("The receive buffer is smaller than the queued packet.", nameof(buf));
        Array.Copy(element.pkt, buf, len);
        seqNo = element.seq_no;
        departureTime = element.departure_time;
        arrivalTime = element.arrival_time;

        if (ReferenceEquals(s.first, s.last))
            s.last = null;
        s.first = element.next;
        if (element.next is not null)
            element.next.prev = null;

        return len;
    }

    public static void g1050_queue_dump(G1050State s, TextWriter? writer = null) {
        ArgumentNullException.ThrowIfNull(s);
        writer ??= Console.Out;
        writer.WriteLine("Queue scanned forewards");
        for (G1050QueueElement? e = s.first; e is not null; e = e.next) {
            writer.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"Seq {e.seq_no,5}, arrival {e.arrival_time,10:F4}, len {e.len,3}"));
        }
        writer.WriteLine("Queue scanned backwards");
        for (G1050QueueElement? e = s.last; e is not null; e = e.prev) {
            writer.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"Seq {e.seq_no,5}, arrival {e.arrival_time,10:F4}, len {e.len,3}"));
        }
    }
}
