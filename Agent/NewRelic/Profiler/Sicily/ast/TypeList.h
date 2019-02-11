#ifndef _SIGPARSE_AST_TYPE_LIST_H_INCLUDED_
#define _SIGPARSE_AST_TYPE_LIST_H_INCLUDED_

#include <memory>
#include <vector>
#include <string>

#include "Type.h"

namespace sicily {
    namespace ast {
        //
        // XXX gut this & replace with unique_ptr
        //
        class TypeList
        {
            public:
                TypeList();
                ~TypeList();

                void Add(TypePtr type);
                TypePtr GetItem(uint16_t i) const;
                uint16_t GetSize() const;

                xstring_t ToString() const;

            private:
                typedef std::vector<TypePtr> types;

                types items_;
        };

        typedef std::shared_ptr<TypeList> TypeListPtr;
    };
};

#endif

