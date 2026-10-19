pipeline {
    agent { label 'parkwise-pr' }

    options {
        skipDefaultCheckout(true)
        timestamps()
        disableConcurrentBuilds()
        timeout(time: 60, unit: 'MINUTES')
    }

    environment {
        CI = 'true'
    }

    stages {
        stage('Verify Pull Request') {
            steps {
                script {
                    if (!(env.CHANGE_ID ==~ /[0-9]+/)) {
                        error('A numeric CHANGE_ID is required for this Pull Request pipeline.')
                    }
                    if (env.CHANGE_TARGET != 'develop') {
                        error("Expected PR targeting develop, got '${env.CHANGE_TARGET ?: 'unknown'}'")
                    }
                    if (!env.CHANGE_BRANCH?.trim() || !env.CHANGE_TITLE?.trim()) {
                        error('CHANGE_BRANCH and CHANGE_TITLE are required for Git Policy validation.')
                    }
                    echo "PR Number: ${env.CHANGE_ID}"
                    echo "PR Source: ${env.CHANGE_BRANCH}"
                    echo "PR Target: ${env.CHANGE_TARGET}"
                }
            }
        }

        stage('Checkout PR') {
            steps {
                checkout scm
                sh 'git log -1 --oneline'
            }
        }

        stage('Git Policy') {
            steps {
                script {
                    def candidateDir = pwd()
                    String repoUrl = scm.userRemoteConfigs[0].url
                    String credentialId = scm.userRemoteConfigs[0].credentialsId
                    if (!repoUrl || !credentialId) {
                        error('GitHub SCM URL and checkout credential are required for trusted policy checkout.')
                    }

                    ws("${candidateDir}@trusted-policy") {
                        deleteDir()
                        try {
                            checkout([
                                $class: 'GitSCM',
                                branches: [[name: '*/develop']],
                                userRemoteConfigs: [[
                                    url: repoUrl,
                                    credentialsId: credentialId,
                                    refspec: '+refs/heads/develop:refs/remotes/origin/develop'
                                ]],
                                extensions: [[$class: 'CloneOption', noTags: true, shallow: false]]
                            ])
                            def policyDir = pwd()
                            def baseSha = sh(script: 'git rev-parse --verify HEAD', returnStdout: true).trim()
                            sh "python3 -m unittest discover -s scripts/ci -p 'test_*.py'"

                            dir(candidateDir) {
                                def prRef = "refs/remotes/origin/PR-${env.CHANGE_ID}^{commit}"
                                def headSha = sh(
                                    script: "git rev-parse --verify '${prRef}'",
                                    returnStdout: true
                                ).trim()
                                def checkoutSha = sh(script: 'git rev-parse --verify HEAD', returnStdout: true).trim()
                                sh "git merge-base --is-ancestor ${headSha} ${checkoutSha}"
                                withEnv([
                                    "POLICY_CHECKOUT=${policyDir}",
                                    "POLICY_HEAD_BRANCH=${env.CHANGE_BRANCH}",
                                    "POLICY_PR_TITLE=${env.CHANGE_TITLE}",
                                    "POLICY_BASE_SHA=${baseSha}",
                                    "POLICY_HEAD_SHA=${headSha}",
                                    'POLICY_CUTOVER_UTC=2026-10-06T01:41:50Z'
                                ]) {
                                    sh 'git fetch --no-tags "$POLICY_CHECKOUT" "$POLICY_BASE_SHA"'
                                    sh 'python3 "$POLICY_CHECKOUT/scripts/ci/check_git_policy.py"'
                                }
                            }
                        } finally {
                            deleteDir()
                        }
                    }
                }
            }
        }

        stage('Backend Restore + Build') {
            steps {
                sh 'dotnet --version'
                sh 'dotnet tool restore'
                sh 'dotnet restore ParkingSystem.slnx -m:1 --disable-build-servers'
                sh 'dotnet build ParkingSystem.slnx --no-restore -m:1 --disable-build-servers'
            }
        }

        stage('Backend Unit Tests') {
            steps {
                sh '''
                    set -e
                    dotnet test tests/Identity/Identity.UnitTests/Identity.UnitTests.csproj --no-build --no-restore
                    dotnet test tests/Parking/Parking.UnitTests/Parking.UnitTests.csproj --no-build --no-restore
                    dotnet test tests/Booking/Booking.UnitTests/Booking.UnitTests.csproj --no-build --no-restore
                    dotnet test tests/Payment/Payment.UnitTests/Payment.UnitTests.csproj --no-build --no-restore
                    dotnet test tests/Notification/Notification.UnitTests/Notification.UnitTests.csproj --no-build --no-restore
                '''
            }
        }

        stage('Architecture Tests') {
            steps {
                sh 'dotnet test tests/ArchitectureTests/ArchitectureTests.csproj --no-build --no-restore'
            }
        }

        stage('Web Foundation Tests') {
            steps {
                sh 'dotnet test tests/WebFoundationTests/WebFoundationTests.csproj --no-build --no-restore'
            }
        }

        stage('Frontend CI') {
            steps {
                dir('web') {
                    sh '''
                        set -e
                        node --version
                        npm --version
                        npm ci
                        rm -rf -- .next
                        npm run lint
                        npm run typecheck
                        npm run format:check
                        npm run test:run
                        npm run build
                    '''
                }
            }
        }

        stage('Python AI CI') {
            steps {
                dir('ai/ai-service') {
                    sh '''
                        set -e
                        python3 --version
                        python3 -m venv --clear .ci-venv
                        .ci-venv/bin/python -m pip install --disable-pip-version-check -r requirements.lock
                        .ci-venv/bin/python -m compileall -q app tests
                        .ci-venv/bin/python -m unittest discover -s tests -v
                    '''
                }
            }
        }
    }

    post {
        success { echo 'ParkWise PR CI: SUCCESS' }
        failure { echo 'ParkWise PR CI: FAILED' }
    }
}
