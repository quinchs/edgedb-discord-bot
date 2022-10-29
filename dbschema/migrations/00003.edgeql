CREATE MIGRATION m1olgkfqnzshwpewiulz62ki32exivrelk552kffhtejehbfxu7f2q
    ONTO m1r74uq3sz6xxfatjvljyk6aoj5ptaik37uozyv2tsstmnoldabrna
{
  ALTER TYPE default::UserSchema {
      ALTER PROPERTY created_at {
          SET TYPE std::datetime USING (<std::datetime>'');
      };
  };
};
