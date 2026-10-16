pipeline {
    agent { label 'parkwise-dotnet10' }

    options {
        skipDefaultCheckout(true)
        timestamps()
        disableConcurrentBuilds()
        timeout(time: 45, unit: 'MINUTES')
    }

    environment {
        CI                                = 'true'
        DOTNET_CLI_TELEMETRY_OPTOUT       = '1'
        DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
        DOTNET_ROOT                       = '/usr/share/dotnet'
        PATH                              = "/usr/share/dotnet:/usr/local/share/dotnet:${env.PATH}"
    }

    stages {
        stage('Checkout & Context') {
            steps {
                script {
                    def checkoutInfo = checkout scm
                    env.CHECKED_OUT_BRANCH = checkoutInfo.GIT_BRANCH ?: env.BRANCH_NAME ?: 'unknown'
                    env.CURRENT_COMMIT     = checkoutInfo.GIT_COMMIT ?: env.GIT_COMMIT   ?: 'HEAD'

                    def isPR = (env.CHANGE_ID != null && env.CHANGE_ID != '') ||
                               (env.BRANCH_NAME?.startsWith('PR-')) ||
                               (env.CHECKED_OUT_BRANCH?.startsWith('PR-'))

                    if (isPR) {
                        def prMatcher  = env.BRANCH_NAME =~ /PR-(\d+)/
                        def prNumber   = env.CHANGE_ID ?: (prMatcher ? prMatcher[0][1] : env.BRANCH_NAME)
                        def prTitle    = env.CHANGE_TITLE ?: 'Pull Request'
                        def target     = env.CHANGE_TARGET ?: 'develop'
                        def source     = env.CHANGE_BRANCH ?: env.CHECKED_OUT_BRANCH

                        currentBuild.displayName  = "#${BUILD_NUMBER} [PR-${prNumber}]"
                        currentBuild.description  = "PR #${prNumber}: ${prTitle}\n${source} → ${target}"

                        echo "========================================================="
                        echo "  PULL REQUEST MODE"
                        echo "  PR #${prNumber}: ${prTitle}"
                        echo "  ${source} → ${target}  @${env.CURRENT_COMMIT.take(8)}"
                        echo "========================================================="
                    } else {
                        def branch = env.BRANCH_NAME ?: env.CHECKED_OUT_BRANCH
                        currentBuild.displayName  = "#${BUILD_NUMBER} [${branch}]"
                        currentBuild.description  = "Branch: ${branch} @${env.CURRENT_COMMIT.take(8)}"

                        echo "========================================================="
                        echo "  BRANCH MODE"
                        echo "  ${branch}  @${env.CURRENT_COMMIT.take(8)}"
                        echo "========================================================="
                    }
                }
            }
        }

        stage('Environment') {
            steps {
                sh '''
                    echo "=== Dotnet SDK ==="
                    which dotnet 2>/dev/null || echo "dotnet not in PATH — checking DOTNET_ROOT"
                    dotnet --info 2>&1 | head -10
                    echo ""
                    echo "=== Workspace ==="
                    pwd
                    df -h . | tail -1
                '''
            }
        }

        stage('Git Policy') {
            when { changeRequest() }
            steps {
                script {
                    def target  = env.CHANGE_TARGET ?: 'develop'
                    def baseSha = sh(
                        script: "git merge-base HEAD origin/${target} 2>/dev/null || git rev-parse origin/${target}",
                        returnStdout: true
                    ).trim()
                    withEnv([
                        "POLICY_HEAD_BRANCH=${env.CHANGE_BRANCH ?: env.BRANCH_NAME}",
                        "POLICY_PR_TITLE=${env.CHANGE_TITLE ?: ''}",
                        "POLICY_BASE_SHA=${baseSha}",
                        "POLICY_HEAD_SHA=${env.CURRENT_COMMIT}",
                        "POLICY_CUTOVER_UTC=2026-01-01T00:00:00Z"
                    ]) {
                        sh 'python3 -I scripts/ci/check_git_policy.py'
                    }
                }
            }
        }

        stage('Restore') {
            steps {
                sh 'dotnet restore ParkingSystem.slnx -m:1 --disable-build-servers'
            }
        }

        stage('Build') {
            steps {
                sh 'dotnet build ParkingSystem.slnx --no-restore -m:1 --disable-build-servers'
            }
        }

        stage('Unit Tests') {
            steps {
                sh '''
                    set -e
                    mkdir -p TestResults
                    FAILED=""

                    run_unit() {
                        NAME="$1"
                        PROJ="$2"
                        TRX="$3"
                        echo ""
                        echo "┌── Unit: ${NAME} ──────────────────────────────────────"
                        if dotnet test "${PROJ}" \
                            --no-build --no-restore \
                            --logger "console;verbosity=normal" \
                            --logger "trx;LogFileName=${TRX}" \
                            --results-directory TestResults; then
                            echo "└── ${NAME}: PASSED"
                        else
                            echo "└── ${NAME}: FAILED ← xem chi tiết bên trên"
                            FAILED="${FAILED} ${NAME}"
                        fi
                    }

                    run_unit "Identity"     "tests/Identity/Identity.UnitTests/Identity.UnitTests.csproj"           "identity-unit.trx"
                    run_unit "Parking"      "tests/Parking/Parking.UnitTests/Parking.UnitTests.csproj"             "parking-unit.trx"
                    run_unit "Booking"      "tests/Booking/Booking.UnitTests/Booking.UnitTests.csproj"             "booking-unit.trx"
                    run_unit "Payment"      "tests/Payment/Payment.UnitTests/Payment.UnitTests.csproj"             "payment-unit.trx"
                    run_unit "Notification" "tests/Notification/Notification.UnitTests/Notification.UnitTests.csproj" "notification-unit.trx"

                    if [ -n "${FAILED}" ]; then
                        echo ""
                        echo "╔══════════════════════════════════════════════════════╗"
                        echo "║  UNIT TEST FAILURES:${FAILED}"
                        echo "╚══════════════════════════════════════════════════════╝"
                        exit 1
                    fi
                '''
            }
        }

        stage('Architecture Tests') {
            steps {
                sh '''
                    echo ""
                    echo "┌── Architecture Rules ──────────────────────────────────"
                    dotnet test tests/ArchitectureTests/ArchitectureTests.csproj \
                        --no-build --no-restore \
                        --logger "console;verbosity=normal" \
                        --logger "trx;LogFileName=architecture-rules.trx" \
                        --results-directory TestResults
                    echo "└── Architecture: PASSED"
                '''
            }
        }

        stage('Web Foundation Tests') {
            steps {
                sh '''
                    echo ""
                    echo "┌── Web Foundation ──────────────────────────────────────"
                    dotnet test tests/WebFoundationTests/WebFoundationTests.csproj \
                        --no-build --no-restore \
                        --logger "console;verbosity=normal" \
                        --logger "trx;LogFileName=web-foundation.trx" \
                        --results-directory TestResults
                    echo "└── Web Foundation: PASSED"
                '''
            }
        }

        stage('AI Service Tests') {
            steps {
                script {
                    def hasPython = sh(
                        script: 'command -v python3 >/dev/null 2>&1 || command -v python >/dev/null 2>&1',
                        returnStatus: true
                    ) == 0
                    if (hasPython && fileExists('ai/ai-service')) {
                        sh '''
                            PYTHON_BIN=$(command -v python3 || command -v python)
                            echo "Using Python: $(${PYTHON_BIN} --version)"
                            ${PYTHON_BIN} -m unittest discover -s ai/ai-service/tests -v
                        '''
                    } else {
                        echo 'Skipping AI tests: Python not found or ai directory absent.'
                    }
                }
            }
        }

        stage('Frontend Checks') {
            steps {
                script {
                    def hasNode = sh(
                        script: 'command -v node >/dev/null 2>&1',
                        returnStatus: true
                    ) == 0
                    if (hasNode && fileExists('web/package.json')) {
                        sh '''
                            echo "Node $(node -v) / npm $(npm -v)"
                            npm --prefix web run lint
                            npm --prefix web run typecheck
                            npm --prefix web run test:run
                        '''
                    } else {
                        echo 'Skipping frontend checks: Node not found or web/package.json absent.'
                    }
                }
            }
        }

        stage('Integration Tests') {
            environment {
                TESTCONTAINERS_HOST_OVERRIDE = 'host.docker.internal'
            }
            steps {
                sh '''
                    set -e
                    mkdir -p TestResults
                    FAILED=""

                    run_it() {
                        NAME="$1"
                        PROJ="$2"
                        TRX="$3"
                        echo ""
                        echo "┌── Integration: ${NAME} ────────────────────────────"
                        if dotnet test "${PROJ}" \
                            --no-build --no-restore --parallel none \
                            --logger "console;verbosity=normal" \
                            --logger "trx;LogFileName=${TRX}" \
                            --results-directory TestResults; then
                            echo "└── ${NAME}: PASSED"
                        else
                            echo "└── ${NAME}: FAILED ← xem chi tiết bên trên"
                            FAILED="${FAILED} ${NAME}"
                        fi
                    }

                    run_it "Identity"     "tests/Identity/Identity.IntegrationTests/Identity.IntegrationTests.csproj"           "identity-integration.trx"
                    run_it "Parking"      "tests/Parking/Parking.IntegrationTests/Parking.IntegrationTests.csproj"             "parking-integration.trx"
                    run_it "Booking"      "tests/Booking/Booking.IntegrationTests/Booking.IntegrationTests.csproj"             "booking-integration.trx"
                    run_it "Payment"      "tests/Payment/Payment.IntegrationTests/Payment.IntegrationTests.csproj"             "payment-integration.trx"
                    run_it "Notification" "tests/Notification/Notification.IntegrationTests/Notification.IntegrationTests.csproj" "notification-integration.trx"

                    if [ -n "${FAILED}" ]; then
                        echo ""
                        echo "╔══════════════════════════════════════════════════════╗"
                        echo "║  INTEGRATION TEST FAILURES:${FAILED}"
                        echo "╚══════════════════════════════════════════════════════╝"
                    exit 1
                    fi
                '''
            }
        }
    }

    post {
        always {
            script {
                catchError(buildResult: 'SUCCESS', stageResult: 'UNSTABLE') {
                    archiveArtifacts allowEmptyArchive: true,
                                     artifacts: 'TestResults/**/*.trx',
                                     fingerprint: true
                }
                catchError(buildResult: 'SUCCESS', stageResult: 'UNSTABLE') {
                    try {
                        mstest testResultsFile: 'TestResults/**/*.trx', keepLongStdio: true
                    } catch (NoSuchMethodError ignored) {
                        echo 'MSTest plugin not installed — TRX files are archived as raw artifacts.'
                    } catch (Exception e) {
                        echo "MSTest publish: ${e.message}"
                    }
                }
            }
            cleanWs notFailBuild: true, deleteDirs: true
        }
        success {
            echo "========================================================="
            echo "  BUILD #${BUILD_NUMBER} PASSED — ALL CHECKS GREEN"
            echo "========================================================="
        }
        failure {
            echo "========================================================="
            echo "  BUILD #${BUILD_NUMBER} FAILED — xem stage đỏ trong log"
            echo "========================================================="
        }
    }
}
