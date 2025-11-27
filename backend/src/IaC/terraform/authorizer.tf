# Custom REQUEST Authorizer
resource "aws_api_gateway_authorizer" "lambda_authorizer" {
  name                   = "${var.environment}-recipe-authorizer"
  rest_api_id           = aws_api_gateway_rest_api.recipe_api.id
  authorizer_uri        = aws_lambda_function.authorizer_lambda.invoke_arn
  authorizer_credentials = aws_iam_role.api_gateway_authorizer_role.arn
  type                  = "REQUEST"
  identity_source       = "method.request.header.Authorization"
  authorizer_result_ttl_in_seconds = 0

  depends_on = [aws_lambda_function.authorizer_lambda]
}