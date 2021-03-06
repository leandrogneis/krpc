#include <gtest/gtest-message.h>
#include <gtest/gtest-test-part.h>

#include <krpc.h>
#include <krpc/encoder.h>
#include <krpc/services/krpc.h>

#include <string>

#include "gtest/gtest.h"

#include "services/test_service.h"
#include "testing_tools.hpp"

TEST(test_encoder, test_encode_procedure_message) {
  uint8_t data[256];
  krpc_call_t call;
  krpc_argument_t arguments[0];
  ASSERT_EQ(KRPC_OK, krpc_call(&call, "ServiceName", "ProcedureName", 0, arguments));
  pb_ostream_t stream = pb_ostream_from_buffer(data, sizeof(data));
  ASSERT_EQ(KRPC_OK, krpc_encode_message_ProcedureCall(&stream, &call.message));
  std::string expected = "0a0b536572766963654e616d65120d50726f6365647572654e616d65";
  ASSERT_EQ(expected, hexlify(data, stream.bytes_written));
}

TEST(test_encoder, test_encode_procedure_message_with_args) {
  uint8_t data[256];
  int32_t x = 42;
  double y = 3.14159;
  krpc_call_t call;
  krpc_argument_t arguments[2];
  ASSERT_EQ(KRPC_OK, krpc_call(&call, "ServiceName", "ProcedureName", 2, arguments));
  ASSERT_EQ(KRPC_OK, krpc_add_argument(&call, 0, &krpc_encode_callback_int32, &x));
  ASSERT_EQ(KRPC_OK, krpc_add_argument(&call, 1, &krpc_encode_callback_double, &y));
  pb_ostream_t stream = pb_ostream_from_buffer(data, sizeof(data));
  ASSERT_EQ(KRPC_OK, krpc_encode_message_ProcedureCall(&stream, &call.message));
  std::string expected =
    "0a0b536572766963654e616d65120d50726f63656475726"
    "54e616d651a031201541a0c080112086e861bf0f9210940";
  ASSERT_EQ(expected, hexlify(data, stream.bytes_written));
}

TEST(test_encoder, test_encode_value) {
  uint8_t data[2];
  pb_ostream_t stream = pb_ostream_from_buffer(data, sizeof(data));
  ASSERT_EQ(KRPC_OK, krpc_encode_uint32(&stream, 300));
  ASSERT_EQ("ac02", hexlify(data, stream.bytes_written));
}

TEST(test_encoder, test_encode_string) {
  uint8_t data[4];
  pb_ostream_t stream = pb_ostream_from_buffer(data, sizeof(data));
  ASSERT_EQ(KRPC_OK, krpc_encode_string(&stream, "foo"));
  ASSERT_EQ("03666f6f", hexlify(data, stream.bytes_written));
}

TEST(test_encoder, test_encode_unicode_string) {
  char in[4];
  unhexlify((uint8_t*)in, "e284a2");
  in[3] = '\0';
  uint8_t data[4];
  pb_ostream_t stream = pb_ostream_from_buffer(data, sizeof(data));
  ASSERT_EQ(KRPC_OK, krpc_encode_string(&stream, in));
  ASSERT_EQ("03e284a2", hexlify(data, stream.bytes_written));
}

TEST(test_encoder, test_encode_object) {
  uint8_t data[2];
  pb_ostream_t stream = pb_ostream_from_buffer(data, sizeof(data));
  krpc_TestService_TestClass_t value = 300;
  ASSERT_EQ(KRPC_OK, krpc_encode_object(&stream, value));
  ASSERT_EQ("ac02", hexlify(data, stream.bytes_written));
}

TEST(test_encoder, test_encode_object_none) {
  uint8_t data[1];
  pb_ostream_t stream = pb_ostream_from_buffer(data, sizeof(data));
  krpc_TestService_TestClass_t value = 0;
  ASSERT_EQ(KRPC_OK, krpc_encode_object(&stream, value));
  ASSERT_EQ("00", hexlify(data, stream.bytes_written));
}
