pipeline {
    agent { label 'parkwise-e2e' }

    options {
        skipDefaultCheckout(true)
        timestamps()
        disableConcurrentBuilds()
        timeout(time: 45, unit: 'MINUTES')
    }

    environment {
        CI = 'true'
    }

    stages {
        stage('Checkout develop') {
            steps {
                script {
                    def checkoutInfo = checkout scm
                    env.CHECKED_OUT_BRANCH = checkoutInfo.GIT_BRANCH ?: ''
                }
            }
        }

        stage('Verify develop branch') {
            steps {
                script {
                    def allowed = ['develop', 'origin/develop', 'refs/heads/develop', 'refs/remotes/origin/develop']
                    if (env.CHANGE_ID || !allowed.contains(env.CHECKED_OUT_BRANCH)) {
                        error("E2E requires a develop branch checkout; got '${env.CHECKED_OUT_BRANCH ?: 'unknown'}'.")
                    }
                    if (env.BRANCH_NAME && env.BRANCH_NAME != 'develop') {
                        error("E2E requires the develop job; got '${env.BRANCH_NAME}'.")
                    }
                }
                sh 'git log -1 --oneline'
            }
        }

        stage('Install frontend dependencies') {
            steps {
                dir('web') {
                    sh '''
                        set -e
                        node --version
                        npm --version
                        npm ci
                    '''
                }
            }
        }

        stage('Playwright E2E') {
            steps {
                dir('web') {
                    sh 'npm run test:e2e -- --workers=1'
                }
            }
        }
    }

    post {
        success { echo 'ParkWise Playwright E2E: SUCCESS' }
        failure { echo 'ParkWise Playwright E2E: FAILED' }
    }
}
