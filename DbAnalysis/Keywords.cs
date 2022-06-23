using System.Collections.Generic;

namespace DbAnalysis
{
	public static class Keywords
	{
		private static Dictionary<string, string> SqlKeywords;		// lowercase
		private static Dictionary<string, string> ExpressionKeywords;		// lowercase

		public static bool IsKeyword (this string LineL)
		{
			return SqlKeywords.ContainsKey (LineL);
		}

		public static string GetExpressionType (this string LineL)
		{
			return ExpressionKeywords.TryGetValue (LineL, out string Type)
				? Type
				: null
				;
		}

		public static bool NotROrT (this string Line)
		{
			return !SqlKeywords.TryGetValue (Line.ToLower (), out string Code)
				|| (Code != "R" && Code != "T")
				;
		}

		public static bool NotROrC (this string Line)
		{
			return !SqlKeywords.TryGetValue (Line.ToLower (), out string Code)
			       || (Code != "R" && Code != "C")
				;
		}

		static Keywords ()
		{
			ExpressionKeywords = new Dictionary<string, string>
			{
				["current_catalog"] = "name",
				["current_date"] = "date",
				["current_role"] = "name",
				["current_schema"] = "name",
				["current_time"] = "time with time zone",
				["current_timestamp"] = "timestamp with time zone",
				["current_user"] = "name",
				["false"] = "boolean",
				["localtime"] = "time without time zone",
				["localtimestamp"] = "timestamp without time zone",
				["null"] = "unknown",
				["session_user"] = "name",
				["true"] = "boolean",
				["user"] = "name",
			};

			SqlKeywords = new Dictionary<string, string>
			{
				// get all keywords
				// sudo -u postgres psql -t -c "SELECT '[\"' || word || '\"] = \"' || catcode || '\",' FROM pg_get_keywords() ORDER BY word;" | xclip -selection clipboard
				["abort"] = "U",
				["absolute"] = "U",
				["access"] = "U",
				["action"] = "U",
				["add"] = "U",
				["admin"] = "U",
				["after"] = "U",
				["aggregate"] = "U",
				["all"] = "R",
				["also"] = "U",
				["alter"] = "U",
				["always"] = "U",
				["analyse"] = "R",
				["analyze"] = "R",
				["and"] = "R",
				["any"] = "R",
				["array"] = "R",
				["as"] = "R",
				["asc"] = "R",
				["assertion"] = "U",
				["assignment"] = "U",
				["asymmetric"] = "R",
				["at"] = "U",
				["attach"] = "U",
				["attribute"] = "U",
				["authorization"] = "T",
				["backward"] = "U",
				["before"] = "U",
				["begin"] = "U",
				["between"] = "C",
				["bigint"] = "C",
				["binary"] = "T",
				["bit"] = "C",
				["boolean"] = "C",
				["both"] = "R",
				["by"] = "U",
				["cache"] = "U",
				["call"] = "U",
				["called"] = "U",
				["cascade"] = "U",
				["cascaded"] = "U",
				["case"] = "R",
				["cast"] = "R",
				["catalog"] = "U",
				["chain"] = "U",
				["char"] = "C",
				["character"] = "C",
				["characteristics"] = "U",
				["check"] = "R",
				["checkpoint"] = "U",
				["class"] = "U",
				["close"] = "U",
				["cluster"] = "U",
				["coalesce"] = "C",
				["collate"] = "R",
				["collation"] = "T",
				["column"] = "R",
				["columns"] = "U",
				["comment"] = "U",
				["comments"] = "U",
				["commit"] = "U",
				["committed"] = "U",
				["concurrently"] = "T",
				["configuration"] = "U",
				["conflict"] = "U",
				["connection"] = "U",
				["constraint"] = "R",
				["constraints"] = "U",
				["content"] = "U",
				["continue"] = "U",
				["conversion"] = "U",
				["copy"] = "U",
				["cost"] = "U",
				["create"] = "R",
				["cross"] = "T",
				["csv"] = "U",
				["cube"] = "U",
				["current"] = "U",
				["current_catalog"] = "R",
				["current_date"] = "R",
				["current_role"] = "R",
				["current_schema"] = "T",
				["current_time"] = "R",
				["current_timestamp"] = "R",
				["current_user"] = "R",
				["cursor"] = "U",
				["cycle"] = "U",
				["data"] = "U",
				["database"] = "U",
				["day"] = "U",
				["deallocate"] = "U",
				["dec"] = "C",
				["decimal"] = "C",
				["declare"] = "U",
				["default"] = "R",
				["defaults"] = "U",
				["deferrable"] = "R",
				["deferred"] = "U",
				["definer"] = "U",
				["delete"] = "U",
				["delimiter"] = "U",
				["delimiters"] = "U",
				["depends"] = "U",
				["desc"] = "R",
				["detach"] = "U",
				["dictionary"] = "U",
				["disable"] = "U",
				["discard"] = "U",
				["distinct"] = "R",
				["do"] = "R",
				["document"] = "U",
				["domain"] = "U",
				["double"] = "U",
				["drop"] = "U",
				["each"] = "U",
				["else"] = "R",
				["enable"] = "U",
				["encoding"] = "U",
				["encrypted"] = "U",
				["end"] = "R",
				["enum"] = "U",
				["escape"] = "U",
				["event"] = "U",
				["except"] = "R",
				["exclude"] = "U",
				["excluding"] = "U",
				["exclusive"] = "U",
				["execute"] = "U",
				["exists"] = "C",
				["explain"] = "U",
				["extension"] = "U",
				["external"] = "U",
				["extract"] = "C",
				["false"] = "R",
				["family"] = "U",
				["fetch"] = "R",
				["filter"] = "U",
				["first"] = "U",
				["float"] = "C",
				["following"] = "U",
				["for"] = "R",
				["force"] = "U",
				["foreign"] = "R",
				["forward"] = "U",
				["freeze"] = "T",
				["from"] = "R",
				["full"] = "T",
				["function"] = "U",
				["functions"] = "U",
				["generated"] = "U",
				["global"] = "U",
				["grant"] = "R",
				["granted"] = "U",
				["greatest"] = "C",
				["group"] = "R",
				["grouping"] = "C",
				["groups"] = "U",
				["handler"] = "U",
				["having"] = "R",
				["header"] = "U",
				["hold"] = "U",
				["hour"] = "U",
				["identity"] = "U",
				["if"] = "U",
				["ilike"] = "T",
				["immediate"] = "U",
				["immutable"] = "U",
				["implicit"] = "U",
				["import"] = "U",
				["in"] = "R",
				["include"] = "U",
				["including"] = "U",
				["increment"] = "U",
				["index"] = "U",
				["indexes"] = "U",
				["inherit"] = "U",
				["inherits"] = "U",
				["initially"] = "R",
				["inline"] = "U",
				["inner"] = "T",
				["inout"] = "C",
				["input"] = "U",
				["insensitive"] = "U",
				["insert"] = "U",
				["instead"] = "U",
				["int"] = "C",
				["integer"] = "C",
				["intersect"] = "R",
				["interval"] = "C",
				["into"] = "R",
				["invoker"] = "U",
				["is"] = "T",
				["isnull"] = "T",
				["isolation"] = "U",
				["join"] = "T",
				["key"] = "U",
				["label"] = "U",
				["language"] = "U",
				["large"] = "U",
				["last"] = "U",
				["lateral"] = "R",
				["leading"] = "R",
				["leakproof"] = "U",
				["least"] = "C",
				["left"] = "T",
				["level"] = "U",
				["like"] = "T",
				["limit"] = "R",
				["listen"] = "U",
				["load"] = "U",
				["local"] = "U",
				["localtime"] = "R",
				["localtimestamp"] = "R",
				["location"] = "U",
				["lock"] = "U",
				["locked"] = "U",
				["logged"] = "U",
				["mapping"] = "U",
				["match"] = "U",
				["materialized"] = "U",
				["maxvalue"] = "U",
				["method"] = "U",
				["minute"] = "U",
				["minvalue"] = "U",
				["mode"] = "U",
				["month"] = "U",
				["move"] = "U",
				["name"] = "U",
				["names"] = "U",
				["national"] = "C",
				["natural"] = "T",
				["nchar"] = "C",
				["new"] = "U",
				["next"] = "U",
				["no"] = "U",
				["none"] = "C",
				["not"] = "R",
				["nothing"] = "U",
				["notify"] = "U",
				["notnull"] = "T",
				["nowait"] = "U",
				["null"] = "R",
				["nullif"] = "C",
				["nulls"] = "U",
				["numeric"] = "C",
				["object"] = "U",
				["of"] = "U",
				["off"] = "U",
				["offset"] = "R",
				["oids"] = "U",
				["old"] = "U",
				["on"] = "R",
				["only"] = "R",
				["operator"] = "U",
				["option"] = "U",
				["options"] = "U",
				["or"] = "R",
				["order"] = "R",
				["ordinality"] = "U",
				["others"] = "U",
				["out"] = "C",
				["outer"] = "T",
				["over"] = "U",
				["overlaps"] = "T",
				["overlay"] = "C",
				["overriding"] = "U",
				["owned"] = "U",
				["owner"] = "U",
				["parallel"] = "U",
				["parser"] = "U",
				["partial"] = "U",
				["partition"] = "U",
				["passing"] = "U",
				["password"] = "U",
				["placing"] = "R",
				["plans"] = "U",
				["policy"] = "U",
				["position"] = "C",
				["preceding"] = "U",
				["precision"] = "C",
				["prepare"] = "U",
				["prepared"] = "U",
				["preserve"] = "U",
				["primary"] = "R",
				["prior"] = "U",
				["privileges"] = "U",
				["procedural"] = "U",
				["procedure"] = "U",
				["procedures"] = "U",
				["program"] = "U",
				["publication"] = "U",
				["quote"] = "U",
				["range"] = "U",
				["read"] = "U",
				["real"] = "C",
				["reassign"] = "U",
				["recheck"] = "U",
				["recursive"] = "U",
				["ref"] = "U",
				["references"] = "R",
				["referencing"] = "U",
				["refresh"] = "U",
				["reindex"] = "U",
				["relative"] = "U",
				["release"] = "U",
				["rename"] = "U",
				["repeatable"] = "U",
				["replace"] = "U",
				["replica"] = "U",
				["reset"] = "U",
				["restart"] = "U",
				["restrict"] = "U",
				["returning"] = "R",
				["returns"] = "U",
				["revoke"] = "U",
				["right"] = "T",
				["role"] = "U",
				["rollback"] = "U",
				["rollup"] = "U",
				["routine"] = "U",
				["routines"] = "U",
				["row"] = "C",
				["rows"] = "U",
				["rule"] = "U",
				["savepoint"] = "U",
				["schema"] = "U",
				["schemas"] = "U",
				["scroll"] = "U",
				["search"] = "U",
				["second"] = "U",
				["security"] = "U",
				["select"] = "R",
				["sequence"] = "U",
				["sequences"] = "U",
				["serializable"] = "U",
				["server"] = "U",
				["session"] = "U",
				["session_user"] = "R",
				["set"] = "U",
				["setof"] = "C",
				["sets"] = "U",
				["share"] = "U",
				["show"] = "U",
				["similar"] = "T",
				["simple"] = "U",
				["skip"] = "U",
				["smallint"] = "C",
				["snapshot"] = "U",
				["some"] = "R",
				["sql"] = "U",
				["stable"] = "U",
				["standalone"] = "U",
				["start"] = "U",
				["statement"] = "U",
				["statistics"] = "U",
				["stdin"] = "U",
				["stdout"] = "U",
				["storage"] = "U",
				["stored"] = "U",
				["strict"] = "U",
				["strip"] = "U",
				["subscription"] = "U",
				["substring"] = "C",
				["support"] = "U",
				["symmetric"] = "R",
				["sysid"] = "U",
				["system"] = "U",
				["table"] = "R",
				["tables"] = "U",
				["tablesample"] = "T",
				["tablespace"] = "U",
				["temp"] = "U",
				["template"] = "U",
				["temporary"] = "U",
				["text"] = "U",
				["then"] = "R",
				["ties"] = "U",
				["time"] = "C",
				["timestamp"] = "C",
				["to"] = "R",
				["trailing"] = "R",
				["transaction"] = "U",
				["transform"] = "U",
				["treat"] = "C",
				["trigger"] = "U",
				["trim"] = "C",
				["true"] = "R",
				["truncate"] = "U",
				["trusted"] = "U",
				["type"] = "U",
				["types"] = "U",
				["unbounded"] = "U",
				["uncommitted"] = "U",
				["unencrypted"] = "U",
				["union"] = "R",
				["unique"] = "R",
				["unknown"] = "U",
				["unlisten"] = "U",
				["unlogged"] = "U",
				["until"] = "U",
				["update"] = "U",
				["user"] = "R",
				["using"] = "R",
				["vacuum"] = "U",
				["valid"] = "U",
				["validate"] = "U",
				["validator"] = "U",
				["value"] = "U",
				["values"] = "C",
				["varchar"] = "C",
				["variadic"] = "R",
				["varying"] = "U",
				["verbose"] = "T",
				["version"] = "U",
				["view"] = "U",
				["views"] = "U",
				["volatile"] = "U",
				["when"] = "R",
				["where"] = "R",
				["whitespace"] = "U",
				["window"] = "R",
				["with"] = "R",
				["within"] = "U",
				["without"] = "U",
				["work"] = "U",
				["wrapper"] = "U",
				["write"] = "U",
				["xml"] = "U",
				["xmlattributes"] = "C",
				["xmlconcat"] = "C",
				["xmlelement"] = "C",
				["xmlexists"] = "C",
				["xmlforest"] = "C",
				["xmlnamespaces"] = "C",
				["xmlparse"] = "C",
				["xmlpi"] = "C",
				["xmlroot"] = "C",
				["xmlserialize"] = "C",
				["xmltable"] = "C",
				["year"] = "U",
				["yes"] = "U",
				["zone"] = "U",
			};
		}
	}
}