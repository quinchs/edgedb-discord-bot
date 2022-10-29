CREATE MIGRATION m1z2mara34cvhcjk5w5ftjfipofvvlh7np7wbuv53gsqk5xaj7h6ia
    ONTO initial
{
  CREATE TYPE default::User {
      CREATE PROPERTY schema_history -> array<tuple<created_at: cal::local_datetime, schema: std::str>>;
      CREATE REQUIRED PROPERTY user_id -> std::str {
          CREATE CONSTRAINT std::exclusive;
      };
      CREATE INDEX ON (.user_id);
      CREATE PROPERTY current_schema := (std::array_get(.schema_history, (std::len(.schema_history) - 1)));
  };
};
