CREATE MIGRATION m1r74uq3sz6xxfatjvljyk6aoj5ptaik37uozyv2tsstmnoldabrna
    ONTO m1z2mara34cvhcjk5w5ftjfipofvvlh7np7wbuv53gsqk5xaj7h6ia
{
  ALTER TYPE default::User {
      DROP PROPERTY current_schema;
  };
  ALTER TYPE default::User {
      DROP PROPERTY schema_history;
  };
  CREATE TYPE default::UserSchema {
      CREATE REQUIRED PROPERTY created_at -> cal::local_datetime;
      CREATE REQUIRED PROPERTY schema -> std::str;
  };
  ALTER TYPE default::User {
      CREATE MULTI LINK schema_history -> default::UserSchema;
  };
  ALTER TYPE default::User {
      CREATE LINK current_schema := (SELECT
          .schema_history ORDER BY
              .created_at DESC
      LIMIT
          1
      );
  };
};
