using NetTopologySuite.Geometries;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TrafficSigns.Domain.Models
{
    [Table("traffic_signs_map")]
    public class OsmRoadSegment
    {
        [Key, Column("segment_id")]
        public long SegmentId { get; set; }
        [Column("osm_id")]
        public long OsmId { get; set; }
        [Column("name")]
        public string? Name { get; set; }
        [Column("highway_id")]
        public int HighwayId { get; set; }
        [Column("oneway_type")]
        public int OnewayType { get; set; }
        [Column("z_order")]
        public int ZOrder { get; set; }
        [Column("way")]
        public LineString Way { get; set; } = null!;
    }
}
