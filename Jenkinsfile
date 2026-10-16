pipeline {
    agent { label 'parkwise-dotnet10' }

    options {
        skipDefaultCheckout(true)
        timestamps()
        disableConcurrentBuilds()
        timeout(time: 45, unit: 'MINUTES')
    }

    environment {
        CI = 'true'
        DOTNET_CLI_TELEMETRY_OPTOUT = '1'
        DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
        DOTNET_ROOT = '/usr/share/dotnet'
        PATH = "/usr/share/dotnet:/usr/local/share/dotnet:${env.PATH}"
    }

    stages {
        stage('Checkout & Context') {
            steps {
                script {
                    def checkoutInfo = checkout scm
                    env.CHECKED_OUT_BRANCH = checkoutInfo.GIT_BRANCH ?: env.BRANCH_NAME ?: 'unknown'
                    env.CURRENT_COMMIT = checkoutInfo.GIT_COMMIT ?: env.GIT_COMMIT ?: 'HEAD'

                    // Tự động phát hiện Pull Request hoặc Branch thông thường
                    def isPullRequest = (env.CHANGE_ID != null && env.CHANGE_ID != '') || 
                                        (env.BRANCH_NAME != null && env.BRANCH_NAME.startsWith('PR-')) ||
                                        (env.CHECKED_OUT_BRANCH != null && env.CHECKED_OUT_BRANCH.startsWith('PR-'))

                    if (isPullRequest) {
                        def prMatcher = env.BRANCH_NAME =~ /PR-(\d+)/
                        def prNumber = env.CHANGE_ID ?: (prMatcher ? prMatcher[0][1] : env.BRANCH_NAME)
                        def prTitle = env.CHANGE_TITLE ?: 'Pull Request'
                        def targetBranch = env.CHANGE_TARGET ?: 'develop'
                        def sourceBranch = env.CHANGE_BRANCH ?: env.CHECKED_OUT_BRANCH

                        currentBuild.displayName = "#${BUILD_NUMBER} [PR-${prNumber}]"
                        currentBuild.description = "PR #${prNumber}: ${prTitle} (${sourceBranch} -> ${targetBranch})"

                        echo "========================================================="
                        echo "  PARKWISE MULTIBRANCH CI: PULL REQUEST MODE"
                        echo "  PR Number     : #${prNumber}"
                        echo "  PR Title      : ${prTitle}"
                        echo "  Source Branch : ${sourceBranch}"
                        echo "  Target Branch : ${targetBranch}"
                        echo "  Commit Hash   : ${env.CURRENT_COMMIT.take(8)}"
                        echo "========================================================="
                    } else {
                        def branchName = env.BRANCH_NAME ?: env.CHECKED_OUT_BRANCH
                        currentBuild.displayName = "#${BUILD_NUMBER} [${branchName}]"
                        currentBuild.description = "Branch: ${branchName} | Commit: ${env.CURRENT_COMMIT.take(8)}"

                        echo "========================================================="
                        echo "  PARKWISE MULTIBRANCH CI: BRANCH INTEGRATION MODE"
                        echo "  Branch Name   : ${branchName}"
                        echo "  Commit Hash   : ${env.CURRENT_COMMIT.take(8)}"
                        echo "========================================================="
                    }
                }
            }
        }

        stage('Restore') {
            steps {
                echo 'Restoring .NET solution dependencies...'
                sh 'dotnet --version'
                sh 'dotnet restore ParkingSystem.slnx -m:1 --disable-build-servers'
            }
        }

        stage('Build') {
            steps {
                echo 'Building .NET solution...'
                sh 'dotnet build ParkingSystem.slnx --no-restore -m:1 --disable-build-servers'
            }
        }

        stage('Unit Tests') {
            steps {
                echo 'Executing Backend Unit Tests...'
                sh '''
                    set -e
                    dotnet test tests/Identity/Identity.UnitTests/Identity.UnitTests.csproj --no-build --no-restore --logger "trx;LogFileName=identity-unit.trx" --results-directory TestResults
                    dotnet test tests/Parking/Parking.UnitTests/Parking.UnitTests.csproj --no-build --no-restore --logger "trx;LogFileName=parking-unit.trx" --results-directory TestResults
                    dotnet test tests/Booking/Booking.UnitTests/Booking.UnitTests.csproj --no-build --no-restore --logger "trx;LogFileName=booking-unit.trx" --results-directory TestResults
                    dotnet test tests/Payment/Payment.UnitTests/Payment.UnitTests.csproj --no-build --no-restore --logger "trx;LogFileName=payment-unit.trx" --results-directory TestResults
                    dotnet test tests/Notification/Notification.UnitTests/Notification.UnitTests.csproj --no-build --no-restore --logger "trx;LogFileName=notification-unit.trx" --results-directory TestResults
                '''
            }
        }

        stage('Architecture Tests') {
            steps {
                echo 'Executing Architecture Rule Tests...'
                sh 'dotnet test tests/ArchitectureTests/ArchitectureTests.csproj --no-build --no-restore --logger "trx;LogFileName=architecture-rules.trx" --results-directory TestResults'
            }
        }

        stage('Web Foundation Tests') {
            steps {
                echo 'Executing Web Foundation Framework Tests...'
                sh 'dotnet test tests/WebFoundationTests/WebFoundationTests.csproj --no-build --no-restore --logger "trx;LogFileName=web-foundation.trx" --results-directory TestResults'
            }
        }

        stage('AI Service Tests') {
            steps {
                script {
                    def hasPython = sh(script: 'command -v python3 >/dev/null 2>&1 || command -v python >/dev/null 2>&1', returnStatus: true) == 0
                    if (hasPython && fileExists('ai/ai-service')) {
                        echo 'Python runtime detected. Executing AI Service Tests...'
                        sh '''
                            set -e
                            PYTHON_BIN=$(command -v python3 || command -v python)
                            $PYTHON_BIN -m unittest discover -s ai/ai-service/tests -v
                        '''
                    } else {
                        echo 'Skipping AI tests: Python runtime is not installed on this agent or ai directory is absent.'
                    }
                }
            }
        }

        stage('Frontend Checks') {
            steps {
                script {
                    def hasNode = sh(script: 'command -v node >/dev/null 2>&1', returnStatus: true) == 0
                    if (hasNode && fileExists('web/package.json')) {
                        echo 'Node.js detected. Running frontend validation (lint, typecheck, tests)...'
                        sh '''
                            set -e
                            node -v
                            npm -v
                            npm --prefix web run lint
                            npm --prefix web run typecheck
                            npm --prefix web run test:run
                        '''
                    } else {
                        echo 'Skipping frontend checks: Node.js is not installed on this agent or web directory is absent.'
                    }
                }
            }
        }

        stage('Integration Tests') {
            environment {
                TESTCONTAINERS_HOST_OVERRIDE = 'host.docker.internal'
            }
            steps {
                echo 'Executing Backend Integration Tests with Isolated Containers...'
                sh '''
                    set -e
                    dotnet test tests/Identity/Identity.IntegrationTests/Identity.IntegrationTests.csproj --no-build --no-restore --parallel none --logger "trx;LogFileName=identity-integration.trx" --results-directory TestResults
                    dotnet test tests/Parking/Parking.IntegrationTests/Parking.IntegrationTests.csproj --no-build --no-restore --parallel none --logger "trx;LogFileName=parking-integration.trx" --results-directory TestResults
                    dotnet test tests/Booking/Booking.IntegrationTests/Booking.IntegrationTests.csproj --no-build --no-restore --parallel none --logger "trx;LogFileName=booking-integration.trx" --results-directory TestResults
                    dotnet test tests/Payment/Payment.IntegrationTests/Payment.IntegrationTests.csproj --no-build --no-restore --parallel none --logger "trx;LogFileName=payment-integration.trx" --results-directory TestResults
                    dotnet test tests/Notification/Notification.IntegrationTests/Notification.IntegrationTests.csproj --no-build --no-restore --parallel none --logger "trx;LogFileName=notification-integration.trx" --results-directory TestResults
                '''
            }
        }
    }

    post {
        success {
            echo "========================================================="
            echo "  BUILD #${BUILD_NUMBER} PASSED SUCCESSFULLY! ALL CHECKS GREEN."
            echo "========================================================="
        }
        failure {
            echo "========================================================="
            echo "  BUILD #${BUILD_NUMBER} FAILED! CHECK CONSOLE OUTPUT FOR DETAILS."
            echo "========================================================="
        }
        always {
            script {
                // Lưu trữ các file kết quả kiểm thử .trx
                catchError(buildResult: 'SUCCESS', stageResult: 'UNSTABLE') {
                    archiveArtifacts allowEmptyArchive: true, artifacts: 'TestResults/*.trx'
                }

                // Hiển thị kết quả kiểm thử trên giao diện Jenkins (nếu có plugin MSTest/xUnit)
                catchError(buildResult: 'SUCCESS', stageResult: 'UNSTABLE') {
                    try {
                        mstest testResultsFile: 'TestResults/*.trx', keepLongStdio: true
                    } catch (NoSuchMethodError e) {
                        echo "MSTest plugin is not installed on Jenkins Controller. Test artifacts are safely archived."
                    } catch (Exception e) {
                        echo "Failed to publish MSTest report: ${e.message}"
                    }
                }
            }

            cleanWs notFailBuild: true, deleteDirs: true
        }
    }
}
