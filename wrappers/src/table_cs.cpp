////////////////////////////////////////////////////////////////////////////
//
// Copyright 2016 Realm Inc.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
////////////////////////////////////////////////////////////////////////////

#include <realm.hpp>
#include "error_handling.hpp"
#include "marshalling.hpp"
#include "realm_export_decls.hpp"

#include <memory>
#include "timestamp_helpers.hpp"
#include <realm/object-store/results.hpp>
#include <realm/object-store/object_accessor.hpp>
#include <realm/object-store/schema.hpp>

using namespace realm;
using namespace realm::binding;

extern "C" {

REALM_EXPORT Object* table_add_empty_object(TableRef& table, SharedRealm& realm, NativeException::Marshallable& ex)
{
    return handle_errors(ex, [&]() {
        realm->verify_in_write();

        Obj obj = table->create_object();
        const std::string object_name(ObjectStore::object_type_for_table_name(table->get_name()));
        auto& object_schema = *realm->schema().find(object_name);
        return new Object(realm, object_schema, obj);
    });
}

}   // extern "C"
