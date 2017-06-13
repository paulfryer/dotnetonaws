var AWS = require('aws-sdk');
const url = require('url');
var stepfunctions = new AWS.StepFunctions();
var lambda = new AWS.Lambda();
var s3 = new AWS.S3();
exports.handler = (event, context, callback) => {

	if (!event.TemplateBucket)
		event.TemplateBucket = "code-build-output";

	var accountId = event.requestContext.accountId;
	var region = context.invokedFunctionArn.split(':')[3];
	var stateMachineArn = "arn:aws:states:" + region + ":" + accountId + ":stateMachine:" + event.path.replace(/\//g, '');

	console.log(stateMachineArn);

	var template = {
		AWSTemplateFormatVersion: "2010-09-09",
		Parameters: {
			LambdaRole: {
				Type: "String",
				Default: "lambda_basic_execution",
				Description: "The Role the Lambda function(s) will assume durring execution."
			}
		},
		Resources: {
			CodeBucket: {
				Type: "AWS::S3::Bucket",
				Properties: {
					BucketName: { "Fn::Join": ["-", ["stepfunction", "codebucket", { "Ref": "AWS::Region" }, { "Ref": "AWS::AccountId" }, { "Ref": "AWS::StackName" }]] }
				}
			},
			StepFunctionRole: {
				Type: "AWS::IAM::Role",
				Properties: {
					AssumeRolePolicyDocument: {
						"Version": "2012-10-17",
						"Statement": [
							{
								"Effect": "Allow",
								"Principal": {
									"Service": { "Fn::Join": ["", ["states.", { "Ref": "AWS::Region" }, ".amazonaws.com"]] }
								},
								"Action": "sts:AssumeRole"
							}
						]
					},
					Policies: [{
						PolicyName: "AllowStepFunctionToExecutionLambda",
						PolicyDocument: {
							"Version": "2012-10-17",
							"Statement": [
								{
									"Effect": "Allow",
									"Action": [
										"lambda:InvokeFunction"
									],
									"Resource": "*"
								}
							]
						}
					}]
				}
			},
			CodeUploadRole: {
				Type: "AWS::IAM::Role",
				Properties: {
					AssumeRolePolicyDocument: {
						Version: "2012-10-17",
						Statement: [{
							Effect: "Allow",
							Principal: {
								Service: ["lambda.amazonaws.com"]
							},
							Action: ["sts:AssumeRole"]
						}]
					},
					Policies: [{
						PolicyName: "AllowCodeUploadToS3",
						PolicyDocument: {
							Version: "2012-10-17",
							Statement: [{
								Effect: "Allow",
								Action: "s3:PutObject",
								Resource: [{ "Fn::Join": ["", [{ "Fn::GetAtt": ["CodeBucket", "Arn"] }, "/*"]] }]
							}]
						}
					}]
				}
			},
			CodeUploadFunction: {
				Type: "AWS::Lambda::Function",
				Properties: {
					Code: {
						ZipFile: {
							"Fn::Join": ["", [
								"var response = require('cfn-response');",
								"var AWS = require('aws-sdk');",
								"var s3 = new AWS.S3(); ",
								"exports.handler = function(event, context) {",
								"  console.log(event.ResourceProperties);",
								" var requestsToProcess = 0; var responseData = {}; ",
								"   for (var p in event.ResourceProperties) { if (p != 'ServiceToken') { requestsToProcess++; console.log('requestToProcess: ' + requestsToProcess);}}",
								"   Object.keys(event.ResourceProperties).forEach(function(p,index) { if (p != 'ServiceToken' && event.ResourceProperties.hasOwnProperty(p)) { ",
								"      var https = require('https'); var codeLocation = event.ResourceProperties[p]; ",
								"      var req = https.request(codeLocation, function(response2) { ",
								"         console.log('Downloading: ' + codeLocation); console.log('property: ' + p); ",
								"         var data = []; response2.on('data', function(chunk) { data.push(chunk); }); response2.on('end', function(){ var zip = Buffer.concat(data); ",
								"              var bucketName = '", { "Fn::GetAtt": ["CodeBucket", "Arn"] }, "'.replace('arn:aws:s3:::', ''); ",
								"              s3.putObject({ Bucket: bucketName, Key: p + '.zip', Body: zip }, function(err1, data1) { console.log(err1); responseData['Bucket_' + p] = bucketName; responseData['Key_' + p] = p + '.zip'; ",
								"              console.log('requestToProcess: ' + requestsToProcess); requestsToProcess--; if (requestsToProcess == 0) response.send(event, context, response.SUCCESS, responseData); }); ",
								"            }); ",
								"       });",
								"      req.end();",
								"    }});",
								"  ",
								"};"
							]]
						}
					},
					Handler: "index.handler",
					Runtime: "nodejs4.3",
					Timeout: "30",
					Role: { "Fn::GetAtt": ["CodeUploadRole", "Arn"] }
				}
			},
			GetCodeLocation: {
				Type: "Custom::GetCodeLocation",
				Properties: {
					"ServiceToken": { "Fn::GetAtt": ["CodeUploadFunction", "Arn"] }
				}
			}
		}
	};
	var params = {
		stateMachineArn: stateMachineArn /* required */
	};
	stepfunctions.describeStateMachine(params, function (err, stateMachineDescription) {
		if (err) console.log(err, err.stack); // an error occurred
		else {
			var stateMachine = JSON.parse(stateMachineDescription.definition);
			var stateMachineName = stateMachineDescription.name.replace(/\W/g, '');
			var smd = stateMachineDescription.definition;
			var definitionParts = smd.split('arn:aws:lambda:');

			//var smd2 = stateMachineDescription.definition.replace((new RegExp(accountId), 'g'), "${AWS::AccountId}");
			//smd2 = smd2.replace((new RegExp(region), 'g'), "${AWS::Region}");

			template.Resources[stateMachineName] = {
				Type: "AWS::StepFunctions::StateMachine",
				Properties: {
					RoleArn: { "Fn::GetAtt": ["StepFunctionRole", "Arn"] }
				},
				DependsOn: []
			};
			var states = Object.keys(stateMachine.States);
			var stateCount = states.length;
			var functionIndex = 0;
			var processingRequests = 0;
			states.forEach(function (stateName) {
				var state = stateMachine.States[stateName];
				if (state.Type == "Task") {
					var lambdaArn = state.Resource;
					processingRequests++;
					lambda.getFunction({ FunctionName: lambdaArn }, function (err, lambdaFunction) {
						processingRequests--;
						if (err) console.log(err, err.stack); // an error occurred
						else {
							var config = lambdaFunction.Configuration;
							var resourceName = config.FunctionName.replace(/\W/g, '');
							template.Resources[stateMachineName].DependsOn.push(resourceName);
							var search = lambdaArn.replace('arn:aws:lambda:', '');
							var i = 0;
							definitionParts.forEach(function (part) {
								if (typeof (part) == "string") {
									definitionParts[i] = definitionParts[i].split(search).join('');
									console.log(search);
								}
								i++;
							});
							functionIndex++;
							var spliceIndex = (functionIndex * 2) - 1;
							definitionParts.splice(spliceIndex, 0, { "Fn::GetAtt": [resourceName, "Arn"] });
							console.log("Adding splice: " + resourceName + ", at: " + spliceIndex);
							//definitionParts.splice(processingRequests + 1, 0, {"Fn::Join": ["", ["arn:aws:lambda:", { "Ref" : "AWS::Region" }, ":", {"Ref": "AWS::AccountId"}, ":function:" + config.FunctionName]]});

							var codeUrl = url.parse(lambdaFunction.Code.Location);
							template.Resources[resourceName] = {
								Type: "AWS::Lambda::Function",
								Properties: {
									FunctionName: config.FunctionName,
									Runtime: config.Runtime,
									Role: {
										"Fn::Join": ['', [
											"arn:aws:iam::",
											{ "Ref": "AWS::AccountId" },
											":role/",
											{ "Ref": "LambdaRole" }]
										]
									},
									Handler: config.Handler,
									Description: config.Description,
									Timeout: config.Timeout,
									MemorySize: config.MemorySize,
									Code: {
										S3Bucket: { "Fn::GetAtt": ["GetCodeLocation", "Bucket_LambdaCodeLocation_" + config.FunctionName] },
										S3Key: { "Fn::GetAtt": ["GetCodeLocation", "Key_LambdaCodeLocation_" + config.FunctionName] }
									}
								}
							};
							template.Resources.GetCodeLocation.Properties["LambdaCodeLocation_" + config.FunctionName] = lambdaFunction.Code.Location;
							var codeLocation = lambdaFunction.Code.Location;
							console.log("Code Location: " + codeLocation);
							if (processingRequests === 0) {
								template.Resources[stateMachineName].Properties.DefinitionString = { "Fn::Join": ["", definitionParts] };
								//template.Resources[stateMachineName].Properties.DefinitionString = smd2;
								var s3Params = {
									Bucket: event.TemplateBucket,
									Key: stateMachineArn,
									Body: JSON.stringify(template),
									ContentType: 'application/json'
								};

								s3.putObject(s3Params, function (e, d) {
									var signParams = { Bucket: event.TemplateBucket, Key: stateMachineArn, Expires: 60 * 10 };
									var signedUrl = s3.getSignedUrl('getObject', signParams);

									var response = {
										statusCode: 302,
										headers: {
											Location: signedUrl
										}
									};

									callback(null, response);
								});
							}
						}
					});
				}
			});
		}
	});
};
