load('/tools/build/protobuf/csharp', 'protobuf_csharp')
load('/tools/build/protobuf/python', 'protobuf_py')
load('/tools/build/protobuf/cpp', 'protobuf_cpp')
load('/tools/build/protobuf/lua', 'protobuf_lua')

package(default_visibility = ['//visibility:public'])

exports_files(['krpc.proto', 'test.proto', 'LICENSE'])

protobuf_csharp(
    name = 'csharp',
    out = 'KRPC.cs',
    src = 'krpc.proto'
)

protobuf_py(
    name = 'py',
    out = 'KRPC.py',
    src = 'krpc.proto'
)

protobuf_cpp(
    name = 'cpp',
    header = 'krpc.pb.hpp',
    source = 'krpc.pb.cpp',
    include = 'krpc.pb.hpp',
    src = 'krpc.proto'
)

protobuf_lua(
    name = 'lua',
    out = 'KRPC.lua',
    src = 'krpc.proto'
)

protobuf_csharp(
    name = 'csharp-test',
    out = 'Test.cs',
    src = 'test.proto'
)

protobuf_py(
    name = 'py-test',
    out = 'Test.py',
    src = 'test.proto'
)

protobuf_lua(
    name = 'lua-test',
    out = 'Test.lua',
    src = 'test.proto'
)