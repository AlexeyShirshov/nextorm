using System;
using System.Linq;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

[Collection("Postgres")]
public sealed class PostgresStep2UnitA2Tests : ProviderTestSuite
{
    protected override ITestProvider Provider => PostgresTestProvider.Instance;

    private IDataContext Context => _sut.DataProvider;

    [Fact]
    public void ScalarStringAndMathFunctions_ShouldCompute()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Left = SqlFunctions.Postgres.left("abc", 2),
                Right = SqlFunctions.Postgres.right("abc", 2),
                Lpad = SqlFunctions.Postgres.lpad("abc", 5),
                Rpad = SqlFunctions.Postgres.rpad("abc", 5),
                Repeat = SqlFunctions.Postgres.repeat("ab", 3),
                Reverse = SqlFunctions.Postgres.reverse("abc"),
                Space = SqlFunctions.Postgres.space(3),
                ConcatWs = SqlFunctions.Postgres.concat_ws(",", "a", "b"),
                Translate = SqlFunctions.Postgres.translate("abc", "ab", "xy"),
                Ascii = SqlFunctions.Postgres.ascii("A"),
                Char = SqlFunctions.Postgres.@char(65),
                BitLength = SqlFunctions.Postgres.bit_length("abc"),
                OctetLength = SqlFunctions.Postgres.octet_length("abc"),
                Cot = SqlFunctions.Postgres.cot(1.0),
                Degrees = SqlFunctions.Postgres.degrees(1.0),
                Radians = SqlFunctions.Postgres.radians(180.0),
                Pi = SqlFunctions.Postgres.pi(),
                Scale = SqlFunctions.Postgres.scale(1.2300m),
                MinScale = SqlFunctions.Postgres.min_scale(1.2300m),
                TrimScale = SqlFunctions.Postgres.trim_scale(1.2300m),
                Btrim = SqlFunctions.Postgres.btrim("  abc  "),
                Ltrim = SqlFunctions.Postgres.ltrim("  abc  ", " "),
                Rtrim = SqlFunctions.Postgres.rtrim("  abc  ", " "),
                StartsWith = SqlFunctions.Postgres.starts_with("abc", "ab"),
                ParseIdent = SqlFunctions.Postgres.parse_ident("a.b", true),
                QuoteIdent = SqlFunctions.Postgres.quote_ident("a b"),
                QuoteLiteral = SqlFunctions.Postgres.quote_literal("a'b"),
                QuoteNullable = SqlFunctions.Postgres.quote_nullable(null),
                ToHex = SqlFunctions.Postgres.to_hex(255),
                ToOct = SqlFunctions.Postgres.to_oct(8),
                ToBin = SqlFunctions.Postgres.to_bin(5),
                Encode = SqlFunctions.Postgres.encode(SqlFunctions.Postgres.decode("616263", "hex"), "hex"),
                Decode = SqlFunctions.Postgres.decode("616263", "hex"),
                Normalize = SqlFunctions.Postgres.normalize("abc", "NFC"),
                Unistr = SqlFunctions.Postgres.unistr("\\0041"),
                RegexpMatch = SqlFunctions.Postgres.regexp_match("abc", "(a)(b)"),
                Div = SqlFunctions.Postgres.div(10m, 3m),
                Erf = SqlFunctions.Postgres.erf(1.0),
                Erfc = SqlFunctions.Postgres.erfc(1.0),
                RandomNormal = SqlFunctions.Postgres.random_normal(0.0, 1.0),
                Acosd = SqlFunctions.Postgres.acosd(0.0),
                Asind = SqlFunctions.Postgres.asind(1.0),
                Atand = SqlFunctions.Postgres.atand(1.0),
                Atan2d = SqlFunctions.Postgres.atan2d(1.0, 1.0),
                Cosd = SqlFunctions.Postgres.cosd(60.0),
                Cotd = SqlFunctions.Postgres.cotd(45.0),
                Sind = SqlFunctions.Postgres.sind(30.0),
                Tand = SqlFunctions.Postgres.tand(45.0)
            })
            .First();

        r.Left.Should().Be("ab");
        r.Right.Should().Be("bc");
        r.Lpad.Should().Be("  abc");
        r.Rpad.Should().Be("abc  ");
        r.Repeat.Should().Be("ababab");
        r.Reverse.Should().Be("cba");
        r.Space.Should().Be("   ");
        r.ConcatWs.Should().Be("a,b");
        r.Translate.Should().Be("xyc");
        r.Ascii.Should().Be(65);
        r.Char.Should().Be("A");
        r.BitLength.Should().Be(24);
        r.OctetLength.Should().Be(3);
        r.Cot.Should().BeApproximately(Math.Cos(1) / Math.Sin(1), 1e-12);
        r.Degrees.Should().BeApproximately(57.29577951308232, 1e-9);
        r.Radians.Should().BeApproximately(Math.PI, 1e-9);
        r.Pi.Should().BeApproximately(Math.PI, 1e-12);
        r.Scale.Should().Be(4);
        r.MinScale.Should().Be(2);
        r.TrimScale.Should().Be(1.23m);
        r.Btrim.Should().Be("abc");
        r.Ltrim.Should().Be("abc  ");
        r.Rtrim.Should().Be("  abc");
        r.StartsWith.Should().BeTrue();
        r.ParseIdent.Should().Equal("a", "b");
        r.QuoteIdent.Should().Be("\"a b\"");
        r.QuoteLiteral.Should().Be("'a''b'");
        r.QuoteNullable.Should().Be("NULL");
        r.ToHex.Should().Be("ff");
        r.ToOct.Should().Be("10");
        r.ToBin.Should().Be("101");
        r.Encode.Should().Be("616263");
        r.Decode.Should().HaveCount(3);
        r.Normalize.Should().Be("abc");
        r.Unistr.Should().Be("A");
        r.RegexpMatch.Should().Equal("a", "b");
        r.Div.Should().Be(3m);
        r.Erf.Should().BeApproximately(0.8427007929497149, 1e-9);
        r.Erfc.Should().BeApproximately(0.1572992070502851, 1e-9);
        r.RandomNormal.Should().BeInRange(-10.0, 10.0);
        r.Acosd.Should().BeApproximately(90.0, 1e-9);
        r.Asind.Should().BeApproximately(90.0, 1e-9);
        r.Atand.Should().BeApproximately(45.0, 1e-9);
        r.Atan2d.Should().BeApproximately(45.0, 1e-9);
        r.Cosd.Should().BeApproximately(0.5, 1e-9);
        r.Cotd.Should().BeApproximately(1.0, 1e-9);
        r.Sind.Should().BeApproximately(0.5, 1e-9);
        r.Tand.Should().BeApproximately(1.0, 1e-9);
    }

    [Fact]
    public void ScalarDateUuidAndBitFunctions_ShouldCompute()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Clock = SqlFunctions.Postgres.clock_timestamp(),
                Statement = SqlFunctions.Postgres.statement_timestamp(),
                Transaction = SqlFunctions.Postgres.transaction_timestamp(),
                TimeOfDay = SqlFunctions.Postgres.timeofday(),
                IsFiniteDate = SqlFunctions.Postgres.isfinite(new DateTime(2023, 5, 17)),
                MakeTsz = SqlFunctions.Postgres.make_timestamptz(2023, 5, 17, 13, 45, 30, "UTC"),
                DateSubtract = SqlFunctions.Postgres.date_subtract(new DateTime(2023, 5, 17), TimeSpan.FromDays(2)),
                Justify = SqlFunctions.Postgres.justify_interval(TimeSpan.FromHours(25)),
                UuidVersion = SqlFunctions.Postgres.uuid_extract_version(new Guid("00000000-0000-4000-8000-000000000000")),
                GetBit = SqlFunctions.Postgres.get_bit(SqlFunctions.Postgres.decode("a1", "hex"), 0L),
                GetByte = SqlFunctions.Postgres.get_byte(SqlFunctions.Postgres.decode("a1", "hex"), 0),
                SetByte = SqlFunctions.Postgres.set_byte(SqlFunctions.Postgres.decode("a1", "hex"), 0, 255),
                BitCount = SqlFunctions.Postgres.bit_count(SqlFunctions.Postgres.decode("a1", "hex")),
                CurrentSchemas = SqlFunctions.Postgres.current_schemas(true),
                Postmaster = SqlFunctions.Postgres.pg_postmaster_start_time(),
                ConfLoad = SqlFunctions.Postgres.pg_conf_load_time(),
                Jit = SqlFunctions.Postgres.pg_jit_available(),
                MyTemp = SqlFunctions.Postgres.pg_my_temp_schema(),
                NotifyUsage = SqlFunctions.Postgres.pg_notification_queue_usage(),
                TriggerDepth = SqlFunctions.Postgres.pg_trigger_depth(),
                CharToEnc = SqlFunctions.Postgres.pg_char_to_encoding("UTF8"),
                EncToChar = SqlFunctions.Postgres.pg_encoding_to_char(6),
                Basetype = SqlFunctions.Postgres.pg_basetype("int4"),
                ToBinLong = SqlFunctions.Postgres.to_bin(123456789L),
                ToHexLong = SqlFunctions.Postgres.to_hex(123456789L),
                ToOctLong = SqlFunctions.Postgres.to_oct(123456789L)
            })
            .First();

        r.Clock.Should().BeAfter(new DateTime(2020, 1, 1));
        r.Statement.Should().BeAfter(new DateTime(2020, 1, 1));
        r.Transaction.Should().BeAfter(new DateTime(2020, 1, 1));
        r.TimeOfDay.Should().NotBeNullOrEmpty();
        r.IsFiniteDate.Should().BeTrue();
        r.MakeTsz.Should().Be(new DateTime(2023, 5, 17, 13, 45, 30, DateTimeKind.Utc));
        r.DateSubtract.Should().Be(new DateTime(2023, 5, 15));
        r.Justify.Should().Be(TimeSpan.FromDays(1) + TimeSpan.FromHours(1));
        r.UuidVersion.Should().Be(4);
        r.GetBit.Should().Be(1);
        r.GetByte.Should().Be(0xa1);
        r.SetByte.Should().HaveCount(1);
        r.BitCount.Should().Be(3);
        r.CurrentSchemas.Should().Contain("public");
        r.Postmaster.Should().BeAfter(new DateTime(2020, 1, 1));
        r.ConfLoad.Should().BeAfter(new DateTime(2020, 1, 1));
        r.Jit.Should().NotBeNull();
        // pg_my_temp_schema() is session-scoped: it returns 0 only when no temporary object was ever
        // created on the pooled physical connection, so assert the OID shape instead of the literal 0.
        r.MyTemp.Should().MatchRegex("^\\d+$");
        r.NotifyUsage.Should().BeInRange(0.0, 1.0);
        r.TriggerDepth.Should().Be(0);
        r.CharToEnc.Should().Be(6);
        r.EncToChar.Should().Be("UTF8");
        r.Basetype.Should().Be("integer");
        r.ToBinLong.Should().NotBeNullOrEmpty();
        r.ToHexLong.Should().NotBeNullOrEmpty();
        r.ToOctLong.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ScalarPgCatalogVisibilityFunctions_ShouldCompute()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Collation = SqlFunctions.Postgres.pg_collation_is_visible("1"),
                Conversion = SqlFunctions.Postgres.pg_conversion_is_visible("1"),
                Function = SqlFunctions.Postgres.pg_function_is_visible("1"),
                OpClass = SqlFunctions.Postgres.pg_opclass_is_visible("1"),
                Operator = SqlFunctions.Postgres.pg_operator_is_visible("1"),
                OpFamily = SqlFunctions.Postgres.pg_opfamily_is_visible("1"),
                StatsObj = SqlFunctions.Postgres.pg_statistics_obj_is_visible("1"),
                Table = SqlFunctions.Postgres.pg_table_is_visible("1"),
                TsConfig = SqlFunctions.Postgres.pg_ts_config_is_visible("1"),
                TsDict = SqlFunctions.Postgres.pg_ts_dict_is_visible("1"),
                TsParser = SqlFunctions.Postgres.pg_ts_parser_is_visible("1"),
                TsTemplate = SqlFunctions.Postgres.pg_ts_template_is_visible("1"),
                Type = SqlFunctions.Postgres.pg_type_is_visible("1"),
                IsOtherTemp = SqlFunctions.Postgres.pg_is_other_temp_schema("1"),
                TablespaceLoc = SqlFunctions.Postgres.pg_tablespace_location("1663"),
                IndexProp = SqlFunctions.Postgres.pg_index_has_property("1", "clustered"),
                IndexAmProp = SqlFunctions.Postgres.pg_indexam_has_property("1", "can_order"),
                IndexColProp = SqlFunctions.Postgres.pg_index_column_has_property("1", 1, "nulls_first"),
                InputValid = SqlFunctions.Postgres.pg_input_is_valid("1", "int4"),
                SerialSeq = SqlFunctions.Postgres.pg_get_serial_sequence("complex_entity", "id"),
                FuncArgs = SqlFunctions.Postgres.pg_get_function_arguments("0"),
                FuncIdent = SqlFunctions.Postgres.pg_get_function_identity_arguments("0"),
                FuncResult = SqlFunctions.Postgres.pg_get_function_result("0"),
                FuncDef = SqlFunctions.Postgres.pg_get_functiondef("0"),
                IndexDef1 = SqlFunctions.Postgres.pg_get_indexdef("0"),
                PartitionDef = SqlFunctions.Postgres.pg_get_partition_constraintdef("0"),
                PartKeyDef = SqlFunctions.Postgres.pg_get_partkeydef("0"),
                RuleDef1 = SqlFunctions.Postgres.pg_get_ruledef("0"),
                StatsObjDef = SqlFunctions.Postgres.pg_get_statisticsobjdef("0"),
                TriggerDef1 = SqlFunctions.Postgres.pg_get_triggerdef("0"),
                UserById = SqlFunctions.Postgres.pg_get_userbyid("0"),
                ViewDef1 = SqlFunctions.Postgres.pg_get_viewdef("0"),
                SnapshotXmax = SqlFunctions.Postgres.pg_snapshot_xmax(SqlFunctions.Postgres.pg_current_snapshot()),
                SnapshotXmin = SqlFunctions.Postgres.pg_snapshot_xmin(SqlFunctions.Postgres.pg_current_snapshot()),
                VisibleInSnap = SqlFunctions.Postgres.pg_visible_in_snapshot("0", SqlFunctions.Postgres.pg_current_snapshot()),
                XactStatus = SqlFunctions.Postgres.pg_xact_status("0"),
                SettingsFlags = SqlFunctions.Postgres.pg_settings_get_flags("max_connections"),
                BlockingPids = SqlFunctions.Postgres.pg_blocking_pids(0),
                SafeBlocking = SqlFunctions.Postgres.pg_safe_snapshot_blocking_pids(0)
            })
            .First();

        r.Collation.Should().BeNull();
        r.Conversion.Should().BeNull();
        r.Function.Should().BeNull();
        r.OpClass.Should().BeNull();
        r.Operator.Should().BeNull();
        r.OpFamily.Should().BeNull();
        r.StatsObj.Should().BeNull();
        r.Table.Should().BeNull();
        r.TsConfig.Should().BeNull();
        r.TsDict.Should().BeNull();
        r.TsParser.Should().BeNull();
        r.TsTemplate.Should().BeNull();
        r.Type.Should().BeNull();
        r.IsOtherTemp.Should().BeFalse();
        r.TablespaceLoc.Should().NotBeNull();
        r.IndexProp.Should().BeNull();
        r.IndexAmProp.Should().BeNull();
        r.IndexColProp.Should().BeNull();
        r.InputValid.Should().BeTrue();
        r.SerialSeq.Should().BeNull();
        r.FuncArgs.Should().BeNull();
        r.FuncIdent.Should().BeNull();
        r.FuncResult.Should().BeNull();
        r.FuncDef.Should().BeNull();
        r.IndexDef1.Should().BeNull();
        r.PartitionDef.Should().BeNull();
        r.PartKeyDef.Should().BeNull();
        r.RuleDef1.Should().BeNull();
        r.StatsObjDef.Should().BeNull();
        r.TriggerDef1.Should().BeNull();
        r.UserById.Should().Be("unknown (OID=0)");
        r.ViewDef1.Should().BeNull();
        r.SnapshotXmax.Should().NotBeNull();
        r.SnapshotXmin.Should().NotBeNull();
        r.VisibleInSnap.Should().NotBeNull();
        r.XactStatus.Should().BeNull();
        r.SettingsFlags.Should().NotBeNull();
        r.BlockingPids.Should().NotBeNull();
        r.SafeBlocking.Should().NotBeNull();
    }

    [Fact]
    public void ScalarTsVectorFunctions_ShouldCompute()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                GetCurrentConfig = SqlFunctions.Postgres.get_current_ts_config(),
                Numnode = SqlFunctions.Postgres.numnode(SqlFunctions.Postgres.to_tsquery("foo")),
                Querytree = SqlFunctions.Postgres.querytree(SqlFunctions.Postgres.to_tsquery("foo")),
                Strip = SqlFunctions.Postgres.strip(SqlFunctions.Postgres.to_tsvector("foo bar")),
                TsvectorToArray = SqlFunctions.Postgres.tsvector_to_array(SqlFunctions.Postgres.to_tsvector("foo bar")),
                TsRewrite3 = SqlFunctions.Postgres.ts_rewrite(SqlFunctions.Postgres.to_tsquery("a & b"), SqlFunctions.Postgres.to_tsquery("a"), SqlFunctions.Postgres.to_tsquery("c")),
                TsQueryPhrase2 = SqlFunctions.Postgres.tsquery_phrase(SqlFunctions.Postgres.to_tsquery("foo"), SqlFunctions.Postgres.to_tsquery("bar")),
                TsQueryPhrase3 = SqlFunctions.Postgres.tsquery_phrase(SqlFunctions.Postgres.to_tsquery("foo"), SqlFunctions.Postgres.to_tsquery("bar"), 3),
                SetWeight2 = SqlFunctions.Postgres.setweight(SqlFunctions.Postgres.to_tsvector("foo"), "A"),
            })
            .First();

        r.GetCurrentConfig.Should().Be("english");
        r.Numnode.Should().BeGreaterThan(0);
        r.Querytree.Should().NotBeNullOrEmpty();
        r.Strip.Should().NotBeNull();
        r.TsvectorToArray.Should().Contain("foo");
        r.TsRewrite3.Should().NotBeNull();
        r.TsQueryPhrase2.Should().Be("'foo' <-> 'bar'");
        r.TsQueryPhrase3.Should().Be("'foo' <3> 'bar'");
        r.SetWeight2.Should().Be("'foo':1A");
    }

    [Fact]
    public void ScalarConvertAndNormalizeFunctions_ShouldCompute()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                ConvertFrom = SqlFunctions.Postgres.convert_from(SqlFunctions.Postgres.decode("616263", "hex"), "UTF8"),
                ConvertTo = SqlFunctions.Postgres.convert_to("abc", "UTF8"),
                NormalizeNfd = SqlFunctions.Postgres.normalize("abc", "NFD"),
                Div2 = SqlFunctions.Postgres.div(10m, 3m)
            })
            .First();

        r.ConvertFrom.Should().Be("abc");
        r.ConvertTo.Should().HaveCount(3);
        r.NormalizeNfd.Should().Be("abc");
        r.Div2.Should().Be(3m);
    }

    [Fact]
    public void ScalarAggregates_ShouldCompute()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                AnyValue = SqlFunctions.Postgres.any_value(x.Id),
                XmlAgg = SqlFunctions.Postgres.xmlagg("<a/>"),
                JsonAggStrict = SqlFunctions.Postgres.json_agg_strict(x.Id),
                JsonbAggStrict = SqlFunctions.Postgres.jsonb_agg_strict(x.Id),
                JsonArrayAgg = SqlFunctions.Postgres.json_arrayagg(x.Id),
                JsonbArrayAgg = SqlFunctions.Postgres.jsonb_arrayagg(x.Id),
                JsonObjectAgg = SqlFunctions.Postgres.json_objectagg("k", x.Id),
                JsonObjectAggUnique = SqlFunctions.Postgres.json_objectagg_unique("k", x.Id),
                JsonbObjectAgg = SqlFunctions.Postgres.jsonb_objectagg("k", x.Id)
            })
            .First();

        r.AnyValue.Should().Be(1);
        r.XmlAgg.Should().Contain("a");
        r.JsonAggStrict.Should().Contain("1");
        r.JsonbAggStrict.Should().Contain("1");
        r.JsonArrayAgg.Should().Contain("1");
        r.JsonbArrayAgg.Should().Contain("1");
        r.JsonObjectAgg.Should().Contain("k");
        r.JsonObjectAggUnique.Should().Contain("k");
        r.JsonbObjectAgg.Should().Contain("k");
    }

    [Fact]
    public void TableFunctions_ShouldCompute()
    {
        var rows = Context.From(() => SqlFunctions.Postgres.generate_series(1L, 3L))
            .OrderBy(r => r.Value)
            .Select(r => r.Value)
            .ToList();
        rows.Should().Equal(new long[] { 1L, 2L, 3L });
    }
}