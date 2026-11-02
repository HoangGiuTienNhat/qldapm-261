-- Catalog metadata only. No application rows, credentials or token values.
BEGIN READ ONLY;
SELECT jsonb_build_object(
  'tables', (SELECT jsonb_agg(jsonb_build_object(
    'name', c.relname,
    'columns', (SELECT jsonb_agg(jsonb_build_object('name',a.attname,'type',format_type(a.atttypid,a.atttypmod),
      'nullable',NOT a.attnotnull,'default',pg_get_expr(d.adbin,d.adrelid)) ORDER BY a.attnum)
      FROM pg_attribute a LEFT JOIN pg_attrdef d ON d.adrelid=a.attrelid AND d.adnum=a.attnum
      WHERE a.attrelid=c.oid AND a.attnum>0 AND NOT a.attisdropped),
    'constraints', (SELECT coalesce(jsonb_agg(jsonb_build_object('name',conname,'definition',pg_get_constraintdef(oid,true)) ORDER BY conname),'[]'::jsonb)
      FROM pg_constraint WHERE conrelid=c.oid),
    'indexes', (SELECT coalesce(jsonb_agg(jsonb_build_object('name',indexname,'definition',indexdef) ORDER BY indexname),'[]'::jsonb)
      FROM pg_indexes WHERE schemaname='public' AND tablename=c.relname),
    'triggers', (SELECT coalesce(jsonb_agg(jsonb_build_object('name',tgname,'definition',pg_get_triggerdef(oid,true)) ORDER BY tgname),'[]'::jsonb)
      FROM pg_trigger WHERE tgrelid=c.oid AND NOT tgisinternal)
  ) ORDER BY c.relname) FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='public' AND c.relkind='r'),
  'functions', (SELECT coalesce(jsonb_agg(jsonb_build_object('name',p.proname,'definition',pg_get_functiondef(p.oid)) ORDER BY p.proname),'[]'::jsonb)
    FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace WHERE n.nspname='public' AND p.prokind='f'),
  'migrations', (SELECT jsonb_agg("MigrationId" ORDER BY "MigrationId") FROM "__EFMigrationsHistory")
);
ROLLBACK;
