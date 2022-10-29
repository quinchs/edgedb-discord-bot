module default {
  type User {
    required property user_id -> str {
      constraint exclusive;
    }
    link current_schema := (select .schema_history order by .created_at desc limit 1);
    multi link schema_history -> UserSchema;
    index on (.user_id)
  }
  type UserSchema {
    required property created_at -> datetime;
    required property schema -> str;
  }
}
